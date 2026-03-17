using Microsoft.SemanticKernel.Memory;

namespace LocalRagSK;

/// <summary>
/// Ingests PDFs into Qdrant via Semantic Kernel's memory abstraction.
///
/// SK's SaveInformationAsync handles embedding generation internally —
/// it calls the Ollama embedding model and stores the resulting vector
/// in Qdrant automatically. You just pass plain text.
///
/// This file is identical to the Azure version — the underlying store
/// (Qdrant vs Azure AI Search) is transparent to this class.
/// </summary>
public class DocumentIngester
{
    private readonly ISemanticTextMemory _memory;
    private readonly AppConfig           _config;

    public DocumentIngester(ISemanticTextMemory memory, AppConfig config)
    {
        _memory = memory;
        _config = config;
    }

    public async Task IngestAsync(string pdfPath)
    {
        Console.WriteLine($"\n── Ingesting: {Path.GetFileName(pdfPath)} ──────────────────");

        // 1. Extract raw text from the PDF
        var text = PdfExtractor.ExtractText(pdfPath);

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("  Warning: No text extracted. Is this a scanned/image-only PDF?");
            return;
        }

        // 2. Split into overlapping chunks
        var chunks = TextChunker.Chunk(
            text,
            Path.GetFileName(pdfPath),
            _config.ChunkSize,
            _config.ChunkOverlap);

        // 3. Save each chunk to Qdrant via SK memory
        //    Internally SK:
        //      a) calls Ollama nomic-embed-text to embed chunk.Text → float[]
        //      b) upserts the vector + text into Qdrant
        Console.WriteLine($"  Embedding and storing {chunks.Count} chunks via Semantic Kernel...");
        Console.WriteLine($"  (Ollama model: {_config.EmbeddingModel} → Qdrant on {_config.QdrantHost}:{_config.QdrantPort})");

        int saved = 0;

        foreach (var chunk in chunks)
        {
            await _memory.SaveInformationAsync(
                collection:         _config.CollectionName,  // Qdrant collection name
                text:               chunk.Text,              // SK embeds this with Ollama
                id:                 chunk.Id,                // unique key — safe to re-ingest
                description:        chunk.Source,            // stored as metadata (filename)
                additionalMetadata: chunk.ChunkIndex.ToString());

            saved++;
            if (saved % 5 == 0 || saved == chunks.Count)
                Console.Write($"\r  Progress: {saved}/{chunks.Count}   ");
        }

        Console.WriteLine();
        Console.WriteLine($"\n  ✓ Done. '{Path.GetFileName(pdfPath)}' is searchable in Qdrant.\n");
    }
}
