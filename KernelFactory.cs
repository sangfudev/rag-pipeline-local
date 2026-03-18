using Microsoft.Extensions.AI;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Builds and configures the Microsoft.Extensions.AI services:
///   - OllamaChatClient        (IChatClient)
///   - OllamaEmbeddingGenerator (IEmbeddingGenerator)
///   - QdrantClient            (vector database via gRPC on port 6334)
/// </summary>
public static class AgentServices
{
    /// <summary>
    /// Creates an IChatClient backed by a local Ollama model.
    /// Uses a 10-minute timeout — local models can be slow, especially on CPU.
    /// </summary>
    public static IChatClient CreateChatClient(AppConfig config)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        return new OllamaChatClient(new Uri(config.OllamaBaseUrl), config.ChatModel, httpClient);
    }

    /// <summary>
    /// Creates an IEmbeddingGenerator backed by Ollama (nomic-embed-text by default).
    /// Must use the same model as ingest — vectors must share the same dimensions.
    /// </summary>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(AppConfig config)
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        return new OllamaEmbeddingGenerator(new Uri(config.OllamaBaseUrl), config.EmbeddingModel, httpClient);
    }

    /// <summary>
    /// Creates a QdrantClient connected via gRPC (port 6334).
    /// </summary>
    public static QdrantClient CreateQdrantClient(AppConfig config)
        => new QdrantClient(config.QdrantHost, config.QdrantPort);
}
