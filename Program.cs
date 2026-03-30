using LocalRagSK;

// ─────────────────────────────────────────────────────────────────────────────
//  LOCAL RAG — Semantic Kernel + Ollama + Qdrant
// ─────────────────────────────────────────────────────────────────────────────
//
//  Prerequisites:
//    1. Qdrant running in Docker:
//         docker-compose up -d
//
//    2. Ollama installed and running with models pulled:
//         ollama pull nomic-embed-text
//         ollama pull phi
//
//  Usage:
//    dotnet run ingest <path-to-pdf> [collection-name]  ← index a PDF into Qdrant
//    dotnet run query  "<question>" [collection-name]   ← single question
//    dotnet run chat   [collection-name]                ← interactive chat with history

AppConfig config;
try
{
    config = AppConfig.Load();
    config.Validate();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\nConfiguration error: {ex.Message}");
    Console.ResetColor();
    return;
}

var command        = args.ElementAtOrDefault(0)?.ToLower();
var argument       = args.ElementAtOrDefault(1);
var collectionName = args.ElementAtOrDefault(2);

var pipeline = new RagPipeline(config);

switch (command)
{
    case "ingest":
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: dotnet run ingest <path-to-pdf> [collection-name]");
            return;
        }
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            Console.Write($"Collection name [{config.CollectionName}]: ");
            var input = Console.ReadLine()?.Trim();
            collectionName = string.IsNullOrWhiteSpace(input) ? config.CollectionName : input;
        }
        await pipeline.IngestAsync(argument, collectionName);
        break;

    case "query":
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: dotnet run query \"your question here\" [collection-name]");
            return;
        }
        var answer = await pipeline.QueryAsync(argument, collectionName);
        Console.WriteLine("\n── Answer ───────────────────────────────────────────────");
        Console.WriteLine(answer);
        Console.WriteLine("────────────────────────────────────────────────────────\n");
        break;

    case "chat":
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            Console.Write($"Collection name [{config.CollectionName}]: ");
            var input = Console.ReadLine()?.Trim();
            collectionName = string.IsNullOrWhiteSpace(input) ? config.CollectionName : input;
        }
        await pipeline.ChatLoopAsync(collectionName);
        break;

    default:
        Console.WriteLine($"""
            Local RAG — Semantic Kernel + Ollama ({config.ChatModel}) + Qdrant
            ────────────────────────────────────────────────────────────────────
            Commands:
              dotnet run ingest <path-to-pdf> [collection-name]   Index a PDF into Qdrant
              dotnet run query  "<question>" [collection-name]    One-shot question and answer
              dotnet run chat   [collection-name]                 Interactive chat with history
            """);
        break;
}
