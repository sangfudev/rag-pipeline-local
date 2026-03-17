using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;

namespace LocalRagSK;

/// <summary>
/// A Semantic Kernel Plugin that exposes Qdrant vector search as a KernelFunction.
///
/// By decorating the method with [KernelFunction], SK can:
///   - Call it automatically during agentic / function-calling flows
///   - Invoke it manually via kernel.InvokeAsync()
///   - Expose it as a tool to the LLM
///
/// The underlying store (Qdrant) is completely hidden behind ISemanticTextMemory.
/// This file is IDENTICAL to the Azure version — only KernelFactory.cs changes.
/// </summary>
public class RagPlugin
{
    private readonly ISemanticTextMemory _memory;
    private readonly AppConfig           _config;

    public RagPlugin(ISemanticTextMemory memory, AppConfig config)
    {
        _memory = memory;
        _config = config;
    }

    /// <summary>
    /// Searches Qdrant for document chunks relevant to the query.
    /// Returns formatted context text ready to inject into a prompt.
    /// </summary>
    [KernelFunction("search_documents")]
    [Description("Searches the local document knowledge base and returns relevant text passages for a given query.")]
    public async Task<string> SearchDocumentsAsync(
        [Description("The search query or question to look up in the documents.")]
        string query)
    {
        Console.WriteLine($"  [RagPlugin] Searching Qdrant for: '{query}'");

        var results = _memory.SearchAsync(
            collection:        _config.CollectionName,
            query:             query,
            limit:             _config.TopK,
            minRelevanceScore: _config.MinRelevance);

        var chunks = new List<string>();

        await foreach (var result in results)
        {
            var source = result.Metadata.Description; // filename stored during ingest
            var text   = result.Metadata.Text;
            var score  = result.Relevance;

            Console.WriteLine($"    [{score:F2}] {source}");
            chunks.Add($"[Source: {source} | Relevance: {score:F2}]\n{text}");
        }

        if (chunks.Count == 0)
            return "No relevant documents found.";

        return string.Join("\n\n---\n\n", chunks);
    }
}
