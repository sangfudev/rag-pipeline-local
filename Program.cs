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
//    dotnet run ingest <path-to-pdf>      ← index a PDF into Qdrant
//    dotnet run query  "<question>"       ← single question
//    dotnet run chat                      ← interactive chat with history

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

var command  = args.ElementAtOrDefault(0)?.ToLower();
var argument = args.ElementAtOrDefault(1);

var pipeline = new RagPipeline(config);

switch (command)
{
    case "ingest":
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: dotnet run ingest <path-to-pdf>");
            return;
        }
        await pipeline.IngestAsync(argument);
        break;

    case "query":
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: dotnet run query \"your question here\"");
            return;
        }
        var answer = await pipeline.QueryAsync(argument);
        Console.WriteLine("\n── Answer ───────────────────────────────────────────────");
        Console.WriteLine(answer);
        Console.WriteLine("────────────────────────────────────────────────────────\n");
        break;

    case "chat":
        await pipeline.ChatLoopAsync();
        break;

    default:
        Console.WriteLine($"""
            Local RAG — Semantic Kernel + Ollama ({config.ChatModel}) + Qdrant
            ────────────────────────────────────────────────────────────────────
            Commands:
              dotnet run ingest <path-to-pdf>      Index a PDF into Qdrant
              dotnet run query  "<question>"       One-shot question and answer
              dotnet run chat                      Interactive chat with history
            """);
        break;
}
