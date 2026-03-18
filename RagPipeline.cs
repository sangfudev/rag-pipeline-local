using System.Diagnostics;
using Microsoft.Extensions.AI;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Orchestrates the full local RAG pipeline using Microsoft.Extensions.AI.
///
/// - IChatClient              → Ollama chat completion (streaming supported)
/// - IEmbeddingGenerator      → Ollama embeddings for query vectorisation
/// - QdrantClient             → vector similarity search
/// - RagPlugin                → retrieval wrapper combining embeddings + Qdrant
/// - DocumentIngester         → PDF ingest pipeline
/// </summary>
public class RagPipeline
{
    private readonly IChatClient                                     _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>>   _embeddingGenerator;
    private readonly QdrantClient                                    _qdrantClient;
    private readonly RagPlugin                                       _ragPlugin;
    private readonly AppConfig                                       _config;

    private const string SystemPrompt = """
        You are a helpful assistant that answers questions based on documents in a knowledge base.
        You will be given retrieved context from those documents along with each question.
        Answer only using the provided context. If the context doesn't contain the answer,
        say so clearly. Be concise, accurate, and cite sources when relevant.
        """;

    public RagPipeline(AppConfig config)
    {
        _config             = config;
        _chatClient         = AgentServices.CreateChatClient(config);
        _embeddingGenerator = AgentServices.CreateEmbeddingGenerator(config);
        _qdrantClient       = AgentServices.CreateQdrantClient(config);
        _ragPlugin          = new RagPlugin(_embeddingGenerator, _qdrantClient, config);
    }

    // ── Ingest ────────────────────────────────────────────────────────────────

    public async Task IngestAsync(string pdfPath)
    {
        var ingester = new DocumentIngester(_embeddingGenerator, _qdrantClient, _config);
        await ingester.IngestAsync(pdfPath);
    }

    // ── Single query ──────────────────────────────────────────────────────────

    /// <summary>
    /// Answers a question in two steps:
    ///   1. Retrieve relevant chunks from Qdrant via RagPlugin
    ///   2. Send context + question to the chat model and return the answer
    /// </summary>
    public async Task<string> QueryAsync(string question)
    {
        Console.WriteLine($"\n── Query ──────────────────────────────────────────────");
        Console.WriteLine($"  Q: {question}");
        Console.WriteLine($"  Retrieving context from Qdrant...");

        var context = await _ragPlugin.SearchDocumentsAsync(question);

        if (context == "No relevant documents found.")
            return "No relevant documents found. Have you ingested any PDFs yet?";

        Console.WriteLine($"  Generating answer with Ollama ({_config.ChatModel})...\n");

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User,
                $"""
                Retrieved context:
                {context}

                Question: {question}

                Answer:
                """)
        };

        var sw = Stopwatch.StartNew();
        var response = await _chatClient.GetResponseAsync(messages);
        sw.Stop();

        PrintStats(sw.Elapsed, response.Usage);

        return response.Text ?? "No answer generated.";
    }

    // ── Interactive chat loop with history ────────────────────────────────────

    /// <summary>
    /// Chat REPL that maintains full conversation history.
    /// Each turn streams tokens to the console while collecting ChatResponseUpdates.
    /// After each response, the updates are coalesced into a ChatResponse to read
    /// the final UsageDetails (input/output token counts).
    /// </summary>
    public async Task ChatLoopAsync()
    {
        Console.WriteLine($"""

            ── Local RAG Chat (Microsoft.Extensions.AI) ────────────
              LLM:        Ollama / {_config.ChatModel}
              Embeddings: Ollama / {_config.EmbeddingModel}
              Vector DB:  Qdrant on {_config.QdrantHost}:{_config.QdrantPort}
              Type your question and press Enter.
              Type 'exit' to quit.
            ────────────────────────────────────────────────────────

            """);

        var history = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt)
        };

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            Console.WriteLine("  Searching Qdrant...");
            var context = await _ragPlugin.SearchDocumentsAsync(input);

            history.Add(new ChatMessage(ChatRole.User,
                $"""
                Context from documents:
                {context}

                Question: {input}
                """));

            Console.Write("\nAssistant: ");

            var fullResponse = new System.Text.StringBuilder();
            var updates      = new List<ChatResponseUpdate>();
            var sw           = Stopwatch.StartNew();

            await foreach (var update in _chatClient.GetStreamingResponseAsync(history))
            {
                Console.Write(update.Text);
                fullResponse.Append(update.Text);
                updates.Add(update);
            }

            sw.Stop();

            // Coalesce streaming updates → ChatResponse to read UsageDetails
            var chatResponse = updates.ToChatResponse();
            Console.WriteLine();
            PrintStats(sw.Elapsed, chatResponse.Usage);

            history.Add(new ChatMessage(ChatRole.Assistant, fullResponse.ToString()));
        }

        Console.WriteLine("\nGoodbye!");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PrintStats(TimeSpan elapsed, UsageDetails? usage)
    {
        var inTok  = usage?.InputTokenCount?.ToString()  ?? "—";
        var outTok = usage?.OutputTokenCount?.ToString() ?? "—";
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{elapsed.TotalSeconds:F1}s | in: {inTok} tok | out: {outTok} tok]");
        Console.ResetColor();
    }
}
