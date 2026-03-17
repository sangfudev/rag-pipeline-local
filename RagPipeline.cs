using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;

namespace LocalRagSK;

/// <summary>
/// Orchestrates the full local RAG pipeline using Semantic Kernel.
///
/// This file is IDENTICAL to the Azure version.
/// The swap from Azure → local is entirely contained in KernelFactory.cs.
///
/// SK handles:
///   - Prompt templating        ({{$context}}, {{$question}})
///   - Plugin registration      (RagPlugin as a KernelFunction)
///   - Chat history management  (ChatHistory)
///   - Streaming responses      (GetStreamingChatMessageContentsAsync)
///
/// Ollama handles:
///   - Chat completion          (mistral / llama3 / phi3 etc.)
///   - Text embedding           (nomic-embed-text)
///
/// Qdrant handles:
///   - Vector storage and cosine similarity search
/// </summary>
public class RagPipeline
{
    private readonly Kernel              _kernel;
    private readonly ISemanticTextMemory _memory;
    private readonly RagPlugin           _ragPlugin;
    private readonly AppConfig           _config;
    private readonly KernelFunction      _answerFunction;

    // SK prompt template — {{$variable}} syntax
    // Instructs the model to stay grounded in retrieved context
    private const string RagPromptTemplate = """
        You are a helpful assistant that answers questions based on provided documents.
        Answer ONLY using the context below. If the answer is not in the context,
        say "I don't have enough information to answer that from the available documents."
        Do not make up information. Be concise and cite the source when possible.

        Retrieved context:
        {{$context}}

        Question: {{$question}}

        Answer:
        """;

    public RagPipeline(AppConfig config)
    {
        _config    = config;
        _kernel    = KernelFactory.CreateKernel(config);
        _memory    = KernelFactory.CreateMemory(config);
        _ragPlugin = new RagPlugin(_memory, config);

        // Register the RAG plugin — SK can now invoke SearchDocumentsAsync
        // as a named function: RagPlugin.search_documents
        _kernel.Plugins.AddFromObject(_ragPlugin, "RagPlugin");

        // Compile the prompt template into a reusable KernelFunction
        _answerFunction = _kernel.CreateFunctionFromPrompt(
            promptTemplate: RagPromptTemplate,
            functionName:   "answer_question",
            description:    "Answers a question using retrieved document context");
    }

    // ── Ingest ────────────────────────────────────────────────────────────────

    public async Task IngestAsync(string pdfPath)
    {
        var ingester = new DocumentIngester(_memory, _config);
        await ingester.IngestAsync(pdfPath);
    }

    // ── Single query ──────────────────────────────────────────────────────────

    /// <summary>
    /// Answers a question in two steps:
    ///   1. Retrieve relevant chunks from Qdrant via RagPlugin
    ///   2. Invoke the SK prompt function with context injected
    /// </summary>
    public async Task<string> QueryAsync(string question)
    {
        Console.WriteLine($"\n── Query ──────────────────────────────────────────────");
        Console.WriteLine($"  Q: {question}");

        // Step 1 — retrieve context from Qdrant
        Console.WriteLine($"  Retrieving context from Qdrant...");
        var context = await _ragPlugin.SearchDocumentsAsync(question);

        if (context == "No relevant documents found.")
            return "No relevant documents found. Have you ingested any PDFs yet?";

        // Step 2 — fill template variables and invoke the SK semantic function
        Console.WriteLine($"  Generating answer with Ollama ({_config.ChatModel})...\n");

        var arguments = new KernelArguments
        {
            ["context"]  = context,
            ["question"] = question
        };

        var result = await _kernel.InvokeAsync(_answerFunction, arguments);
        return result.GetValue<string>() ?? "No answer generated.";
    }

    // ── Interactive chat loop with history ────────────────────────────────────

    /// <summary>
    /// Chat REPL that maintains full conversation history via SK's ChatHistory.
    /// Each turn retrieves fresh context from Qdrant for the new question,
    /// then includes it alongside the full conversation history so the model
    /// can handle follow-up questions naturally.
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

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history     = new ChatHistory();

        history.AddSystemMessage("""
            You are a helpful assistant that answers questions based on documents in a knowledge base.
            You will be given retrieved context from those documents along with each question.
            Answer only using the provided context. If the context doesn't contain the answer,
            say so clearly. Be concise, accurate, and cite sources when relevant.
            """);

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            // Retrieve context specific to this question
            Console.WriteLine("  Searching Qdrant...");
            var context = await _ragPlugin.SearchDocumentsAsync(input);

            // Compose user turn with injected context
            // Keeps the chat history readable while grounding each answer
            var userMessage = $"""
                Context from documents:
                {context}

                Question: {input}
                """;

            history.AddUserMessage(userMessage);

            Console.Write("\nAssistant: ");

            var fullResponse = new System.Text.StringBuilder();

            // Stream response token-by-token — essential for local models
            // which can be slow to generate, especially on CPU
            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history))
            {
                Console.Write(chunk.Content);
                fullResponse.Append(chunk.Content);
            }

            Console.WriteLine();

            // Record assistant turn in history for follow-up question awareness
            history.AddAssistantMessage(fullResponse.ToString());
        }

        Console.WriteLine("\nGoodbye!");
    }
}
