using Microsoft.Extensions.AI;
using Qdrant.Client;

namespace LocalRagSK;

/// <summary>
/// Performs vector similarity search against Qdrant.
/// Embeds the query with IEmbeddingGenerator, searches the collection,
/// and returns formatted context text ready to inject into a prompt.
/// </summary>
public class RagPlugin
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly QdrantClient _qdrantClient;
    private readonly AppConfig    _config;

    public RagPlugin(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        QdrantClient qdrantClient,
        AppConfig config)
    {
        _embeddingGenerator = embeddingGenerator;
        _qdrantClient       = qdrantClient;
        _config             = config;
    }

    /// <summary>
    /// Searches the vector store for chunks relevant to the query.
    /// Returns a formatted string of matching passages with source and relevance score.
    /// </summary>
    public async Task<string> SearchDocumentsAsync(string query)
    {
        Console.WriteLine($"  [RagPlugin] Searching Qdrant for: '{query}'");

        var vector  = (await _embeddingGenerator.GenerateAsync([query]))[0].Vector;
        var results = await _qdrantClient.SearchAsync(
            collectionName: _config.CollectionName,
            vector:         vector.ToArray(),
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
