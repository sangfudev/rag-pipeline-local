using System.Diagnostics;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Orchestrates the full local RAG pipeline using Semantic Kernel.
///
/// - IChatCompletionService         → Ollama chat completion (streaming supported)
/// - ITextEmbeddingGenerationService → Ollama embeddings for query vectorisation
/// - QdrantClient                   → vector similarity search
/// - RagPlugin                      → retrieval wrapper combining embeddings + Qdrant
/// - DocumentIngester               → PDF ingest pipeline
/// </summary>
public class RagPipeline
{
    private readonly IChatCompletionService          _chatService;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient                    _qdrantClient;
    private readonly RagPlugin                       _ragPlugin;
    private readonly AppConfig                       _config;

    private const string SystemPrompt = """
        You are a helpful assistant that answers questions based on documents in a knowledge base.
        You will be given retrieved context from those documents along with each question.
        Answer only using the provided context. If the context doesn't contain the answer,
        say so clearly. Be concise, accurate, and cite sources when relevant.
        """;

    public RagPipeline(AppConfig config)
    {
        _config = config;

        var kernel        = KernelFactory.CreateKernel(config);
        _chatService      = kernel.GetRequiredService<IChatCompletionService>();
        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _qdrantClient     = KernelFactory.CreateQdrantClient(config);
        _ragPlugin        = new RagPlugin(_embeddingService, _qdrantClient, config);
    }

    // ── Ingest ────────────────────────────────────────────────────────────────

    public async Task IngestAsync(string pdfPath)
    {
        var ingester = new DocumentIngester(_embeddingService, _qdrantClient, _config);
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

        var history = new ChatHistory(SystemPrompt);
        history.AddUserMessage(
            $"""
            Retrieved context:
            {context}

            Question: {question}

            Answer:
            """);

        var sw       = Stopwatch.StartNew();
        var response = await _chatService.GetChatMessageContentAsync(history);
        sw.Stop();

        PrintStats(sw.Elapsed, response.Metadata);

        return response.Content ?? "No answer generated.";
    }

    // ── Interactive chat loop with history ────────────────────────────────────

    /// <summary>
    /// Chat REPL that maintains full conversation history.
    /// Each turn streams tokens to the console in real time.
    /// Token counts are read from the last streaming chunk's metadata.
    /// </summary>
    public async Task ChatLoopAsync()
    {
        Console.WriteLine($"""

            ── Local RAG Chat (Semantic Kernel) ────────────────────
              LLM:        Ollama / {_config.ChatModel}
              Embeddings: Ollama / {_config.EmbeddingModel}
              Vector DB:  Qdrant on {_config.QdrantHost}:{_config.QdrantPort}
              Type your question and press Enter.
              Type 'exit' to quit.
            ────────────────────────────────────────────────────────

            """);

        var history = new ChatHistory(SystemPrompt);

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            Console.WriteLine("  Searching Qdrant...");
            var context = await _ragPlugin.SearchDocumentsAsync(input);

            history.AddUserMessage(
                $"""
                Context from documents:
                {context}

                Question: {input}
                """);

            Console.Write("\nAssistant: ");

            var fullResponse = new StringBuilder();
            var sw           = Stopwatch.StartNew();
            IReadOnlyDictionary<string, object?>? lastMetadata = null;

            await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(history))
            {
                Console.Write(chunk.Content);
                fullResponse.Append(chunk.Content);
                if (chunk.Metadata is not null)
                    lastMetadata = chunk.Metadata;
            }

            sw.Stop();
            Console.WriteLine();
            PrintStats(sw.Elapsed, lastMetadata);

            history.AddAssistantMessage(fullResponse.ToString());
        }

        Console.WriteLine("\nGoodbye!");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void PrintStats(TimeSpan elapsed, IReadOnlyDictionary<string, object?>? metadata)
    {
        // Ollama exposes prompt_eval_count / eval_count in response metadata.
        // Keys are surfaced by the SK Ollama connector as PascalCase strings.
        var inTok  = TryGetMeta(metadata, "PromptEvalCount", "PromptTokenCount") ?? "—";
        var outTok = TryGetMeta(metadata, "EvalCount", "CompletionTokenCount")   ?? "—";

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"  [{elapsed.TotalSeconds:F1}s | in: {inTok} tok | out: {outTok} tok]");
        Console.ResetColor();
    }

    private static string? TryGetMeta(IReadOnlyDictionary<string, object?>? metadata, params string[] keys)
    {
        if (metadata is null) return null;
        foreach (var key in keys)
            if (metadata.TryGetValue(key, out var val) && val is not null)
                return val.ToString();
        return null;
    }
}
