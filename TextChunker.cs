namespace LocalRagSK;

/// <summary>
/// Splits document text into overlapping word-based chunks.
/// Overlap preserves context at chunk boundaries — important for retrieval quality.
/// </summary>
public static class TextChunker
{
    public static List<TextChunk> Chunk(
        string text,
        string sourceFileName,
        int chunkSize    = 400,
        int chunkOverlap = 50)
    {
        var words = text
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var chunks     = new List<TextChunk>();
        int i          = 0;
        int chunkIndex = 0;

        while (i < words.Length)
        {
            int count     = Math.Min(chunkSize, words.Length - i);
            var chunkText = string.Join(" ", words, i, count);

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(new TextChunk
                {
                    // ID must be unique per chunk — used as the Qdrant point key via SK
                    Id         = $"{Path.GetFileNameWithoutExtension(sourceFileName)}-{chunkIndex}",
                    Text       = chunkText,
                    Source     = sourceFileName,
                    ChunkIndex = chunkIndex
                });
                chunkIndex++;
            }

            i += Math.Max(1, chunkSize - chunkOverlap);
        }

        Console.WriteLine($"  Split into {chunks.Count} chunks " +
                          $"(size={chunkSize} words, overlap={chunkOverlap} words).");
        return chunks;
    }
}

public record TextChunk
{
    public required string Id         { get; init; }
    public required string Text       { get; init; }
    public required string Source     { get; init; }
    public required int    ChunkIndex { get; init; }
}
