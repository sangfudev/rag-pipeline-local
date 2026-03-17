using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;

namespace LocalRagSK;

/// <summary>
/// Builds and configures the Semantic Kernel instance with:
///   - Ollama chat completion  (local LLM — mistral, llama3, phi3, etc.)
///   - Ollama text embeddings  (local embedding model — nomic-embed-text)
///   - Qdrant vector memory    (local vector database)
///
/// This is the ONLY file that differs from the Azure version.
/// Every other file — RagPipeline, RagPlugin, DocumentIngester — is identical
/// because SK abstracts the underlying services behind common interfaces.
/// </summary>
public static class KernelFactory
{
    /// <summary>
    /// Creates a Kernel with Ollama chat completion and embedding generation.
    /// No API keys needed — Ollama runs entirely on localhost.
    /// </summary>
    public static Kernel CreateKernel(AppConfig config)
    {
        var builder = Kernel.CreateBuilder();

        // Local models can be slow — use a generous timeout instead of the default 100s
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.OllamaBaseUrl),
            Timeout     = TimeSpan.FromMinutes(10)
        };

        // Chat completion — talks to Ollama's /api/generate endpoint
        builder.AddOllamaChatCompletion(
            modelId:    config.ChatModel,
            httpClient: httpClient);

        // Text embedding generation — talks to Ollama's /api/embeddings endpoint
        builder.AddOllamaTextEmbeddingGeneration(
            modelId:    config.EmbeddingModel,
            httpClient: httpClient);

        return builder.Build();
    }

    /// <summary>
    /// Creates an ISemanticTextMemory backed by Qdrant.
    /// SK uses this for SaveInformationAsync (ingest) and SearchAsync (retrieval).
    ///
    /// QdrantMemoryStore creates the collection automatically on first use
    /// if it does not already exist.
    /// </summary>
    public static ISemanticTextMemory CreateMemory(AppConfig config)
    {
        // Qdrant vector store — connects via gRPC on port 6334
        var memoryStore = new QdrantMemoryStore(
            endpoint: $"http://{config.QdrantHost}:{config.QdrantPort}",
            vectorSize: config.VectorDimensions);

        // Embedding service used by the memory layer to embed text before storage/search
        // Must use the same model as ingest — vectors must be same dimensions
        var embeddingService = new OllamaTextEmbeddingGenerationService(
            modelId: config.EmbeddingModel,
            endpoint: new Uri(config.OllamaBaseUrl));

        // SemanticTextMemory combines the store + embedding service
        // into SK's unified ISemanticTextMemory interface
        return new SemanticTextMemory(memoryStore, embeddingService);
    }
}
