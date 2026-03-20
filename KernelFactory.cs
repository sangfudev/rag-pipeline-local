using Microsoft.SemanticKernel;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Builds and configures Semantic Kernel with Ollama connectors:
///   - OllamaChatCompletion           (IChatCompletionService)
///   - OllamaTextEmbeddingGeneration  (ITextEmbeddingGenerationService)
///   - QdrantClient                   (vector database via gRPC on port 6334)
/// </summary>
public static class KernelFactory
{
    /// <summary>
    /// Creates a Kernel pre-configured with Ollama chat and embedding services.
    /// Uses a 10-minute timeout — local models can be slow, especially on CPU.
    /// </summary>
    public static Kernel CreateKernel(AppConfig config)
    {
        // The HttpClient overload uses BaseAddress as the Ollama endpoint.
        // A 10-minute timeout is set here because local models can be slow on CPU.
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.OllamaBaseUrl),
            Timeout     = TimeSpan.FromMinutes(10)
        };

        return Kernel.CreateBuilder()
            .AddOllamaChatCompletion(config.ChatModel, httpClient)
            .AddOllamaTextEmbeddingGeneration(config.EmbeddingModel, httpClient)
            .Build();
    }

    /// <summary>
    /// Creates a QdrantClient connected via gRPC (port 6334).
    /// </summary>
    public static QdrantClient CreateQdrantClient(AppConfig config)
        => new QdrantClient(config.QdrantHost, config.QdrantPort);
}
