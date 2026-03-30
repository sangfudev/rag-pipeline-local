using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Performs vector similarity search against Qdrant.
/// Embeds the query with ITextEmbeddingGenerationService, searches the collection,
/// and returns formatted context text ready to inject into a prompt.
/// </summary>
public class RagPlugin
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    private readonly AppConfig    _config;

    public RagPlugin(
        ITextEmbeddingGenerationService embeddingService,
        QdrantClient qdrantClient,
        AppConfig config)
    {
        _embeddingService = embeddingService;
        _qdrantClient     = qdrantClient;
        _config           = config;
    }

    /// <summary>
    /// Searches the vector store for chunks relevant to the query.
    /// Returns a formatted string of matching passages with source and relevance score.
    /// </summary>
    public async Task<string> SearchDocumentsAsync(string query, string? collectionName = null)
    {
        Console.WriteLine($"  [RagPlugin] Searching Qdrant for: '{query}'");

        var collection = collectionName ?? _config.CollectionName;
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync([query]);
        var vector     = embeddings[0].ToArray();

        var results = await _qdrantClient.SearchAsync(
            collectionName: collection,
            vector:         vector,
            limit:          (ulong)_config.TopK,
            scoreThreshold: (float)_config.MinRelevance);

        if (results.Count == 0)
            return "No relevant documents found.";

        var chunks = new List<string>();

        foreach (var result in results)
        {
            var source = result.Payload.TryGetValue("source", out var s) ? s.StringValue : "Unknown";
            var text   = result.Payload.TryGetValue("text",   out var t) ? t.StringValue : string.Empty;
            var score  = result.Score;

            Console.WriteLine($"    [{score:F2}] {source}");
            chunks.Add($"[Source: {source} | Relevance: {score:F2}]\n{text}");
        }

        return string.Join("\n\n---\n\n", chunks);
    }
}
