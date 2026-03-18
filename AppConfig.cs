using Microsoft.Extensions.Configuration;

namespace LocalRagSK;

/// <summary>
/// Strongly-typed configuration loaded from appsettings.json.
/// No secrets needed — everything points to localhost.
/// </summary>
public class AppConfig
{
    // ── Ollama ────────────────────────────────────────────────────────────────
    public string OllamaBaseUrl     { get; init; } = "http://localhost:11434";
    public string ChatModel         { get; init; } = "mistral";
    public string EmbeddingModel    { get; init; } = "nomic-embed-text";

    // ── Qdrant ────────────────────────────────────────────────────────────────
    public string QdrantHost        { get; init; } = "localhost";
    public int    QdrantPort        { get; init; } = 6333;
    public string CollectionName    { get; init; } = "documents";

    // ── RAG tuning ────────────────────────────────────────────────────────────
    public int    VectorDimensions  { get; init; } = 768;   // nomic-embed-text = 768
    public int    ChunkSize         { get; init; } = 500;
    public int    ChunkOverlap      { get; init; } = 75;
    public int    TopK              { get; init; } = 4;
    public double MinRelevance      { get; init; } = 0.75d;

    public static AppConfig Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return new AppConfig
        {
            OllamaBaseUrl    = configuration["Ollama:BaseUrl"]        ?? "http://localhost:11434",
            ChatModel        = configuration["Ollama:ChatModel"]       ?? "mistral",
            EmbeddingModel   = configuration["Ollama:EmbeddingModel"]  ?? "nomic-embed-text",

            QdrantHost       = configuration["Qdrant:Host"]            ?? "localhost",
            QdrantPort       = int.Parse(configuration["Qdrant:Port"]  ?? "6334"),
            CollectionName   = configuration["Qdrant:CollectionName"]  ?? "documents",

            VectorDimensions = int.Parse(configuration["Rag:VectorDimensions"] ?? "768"),
            ChunkSize        = int.Parse(configuration["Rag:ChunkSize"]        ?? "400"),
            ChunkOverlap     = int.Parse(configuration["Rag:ChunkOverlap"]     ?? "50"),
            TopK             = int.Parse(configuration["Rag:TopK"]             ?? "5"),
            MinRelevance     = double.Parse(configuration["Rag:MinRelevance"]  ?? "0.5", System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OllamaBaseUrl))  errors.Add("Ollama:BaseUrl is required");
        if (string.IsNullOrWhiteSpace(ChatModel))      errors.Add("Ollama:ChatModel is required");
        if (string.IsNullOrWhiteSpace(EmbeddingModel)) errors.Add("Ollama:EmbeddingModel is required");
        if (string.IsNullOrWhiteSpace(QdrantHost))     errors.Add("Qdrant:Host is required");
        if (VectorDimensions <= 0)                     errors.Add("Rag:VectorDimensions must be > 0");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Configuration errors:\n  {string.Join("\n  ", errors)}");
    }
}
