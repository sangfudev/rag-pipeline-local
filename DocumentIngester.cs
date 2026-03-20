using Microsoft.SemanticKernel.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace LocalRagSK;

/// <summary>
/// Ingests PDFs into Qdrant via Semantic Kernel's ITextEmbeddingGenerationService.
///
/// Process per chunk:
///   1. Call ITextEmbeddingGenerationService to embed the text via Ollama
///   2. Build a PointStruct with the vector + payload metadata
///   3. Batch-upsert all points into Qdrant
///
/// Uses a deterministic SHA-256-derived point ID so re-ingesting
/// the same file safely overwrites existing vectors rather than duplicating them.
/// </summary>
public class DocumentIngester
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly QdrantClient _qdrantClient;
    private readonly AppConfig    _config;

    public DocumentIngester(
        ITextEmbeddingGenerationService embeddingService,
        QdrantClient qdrantClient,
        AppConfig config)
    {
        _embeddingService = embeddingService;
        _qdrantClient     = qdrantClient;
        _config           = config;
    }

    public async Task IngestAsync(string pdfPath)
    {
        Console.WriteLine($"\n── Ingesting: {Path.GetFileName(pdfPath)} ──────────────────");

        var text = PdfExtractor.ExtractText(pdfPath);

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("  Warning: No text extracted. Is this a scanned/image-only PDF?");
            return;
        }

        var chunks = TextChunker.Chunk(
            text,
            Path.GetFileName(pdfPath),
            _config.ChunkSize,
            _config.ChunkOverlap);

        await EnsureCollectionAsync();

        Console.WriteLine($"  Embedding and storing {chunks.Count} chunks via Semantic Kernel...");
        Console.WriteLine($"  (Ollama model: {_config.EmbeddingModel} → Qdrant on {_config.QdrantHost}:{_config.QdrantPort})");

        int saved = 0;
        var points = new List<PointStruct>(chunks.Count);

        foreach (var chunk in chunks)
        {
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync([chunk.Text]);
            var vector     = embeddings[0].ToArray();

            var point = new PointStruct
            {
                Id      = DeterministicId(chunk.Id),
                Vectors = vector
            };
            point.Payload["id"]         = chunk.Id;
            point.Payload["text"]       = chunk.Text;
            point.Payload["source"]     = chunk.Source;
            point.Payload["chunkIndex"] = chunk.ChunkIndex;

            points.Add(point);

            saved++;
            if (saved % 5 == 0 || saved == chunks.Count)
                Console.Write($"\r  Progress: {saved}/{chunks.Count}   ");
        }

        await _qdrantClient.UpsertAsync(_config.CollectionName, points);

        Console.WriteLine();
        Console.WriteLine($"\n  Done. '{Path.GetFileName(pdfPath)}' is searchable in Qdrant.\n");
    }

    private async Task EnsureCollectionAsync()
    {
        var collections = await _qdrantClient.ListCollectionsAsync();
        if (collections.Any(c => c == _config.CollectionName))
            return;

        await _qdrantClient.CreateCollectionAsync(
            _config.CollectionName,
            new VectorParams
            {
                Size     = (ulong)_config.VectorDimensions,
                Distance = Distance.Cosine
            });
    }

    /// <summary>
    /// Derives a stable ulong point ID from the chunk's string ID using SHA-256.
    /// Re-ingesting the same file produces the same IDs, enabling safe overwrites.
    /// </summary>
    private static ulong DeterministicId(string chunkId)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(chunkId);
        var hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        return BitConverter.ToUInt64(hash, 0);
    }
}
