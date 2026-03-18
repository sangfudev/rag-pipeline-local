# Local RAG — Microsoft.Extensions.AI + Ollama + Qdrant

A fully local RAG pipeline built on **Microsoft.Extensions.AI** (Microsoft Agent Framework).
No Azure. No API keys. No cloud costs.

**Note: running everything locally comes with a performance tradeoff. Retrieving a response from a query might take up to 10 minutes.**

---

## Stack

| Layer | Technology |
|---|---|
| **AI abstractions** | Microsoft.Extensions.AI 9.6.0 (`IChatClient`, `IEmbeddingGenerator`) |
| **Chat LLM** | Ollama / mistral (or any Ollama model) |
| **Embeddings** | Ollama / nomic-embed-text (768 dimensions) |
| **Vector store** | Qdrant (Docker, gRPC port 6334) |
| **PDF extraction** | PdfPig |

---

## Prerequisites

### 1. Docker
Used to run Qdrant.

### 2. Ollama
Download from https://ollama.com, then pull the required models:

```bash
ollama pull nomic-embed-text   # embedding model — 768 dimensions
ollama pull mistral            # chat model (or llama3, phi3, gemma2, etc.)
```

---

## Setup

```bash
# Start Qdrant
docker-compose up -d

# Verify Ollama is running
ollama list
```

Qdrant dashboard: http://localhost:6333/dashboard

---

## Usage

```bash
# Index one or more PDFs
dotnet run ingest "my-document.pdf"
dotnet run ingest "another-document.pdf"

# Ask a single question
dotnet run query "What are the main conclusions?"

# Interactive chat with conversation history
dotnet run chat
```

Each response prints timing and token usage in dark gray:

```
  [12.3s | in: 450 tok | out: 123 tok]
```

---

## Project Structure

```
LocalRagSK/
├── Program.cs            — Entry point, command routing
├── AppConfig.cs          — Strongly-typed config from appsettings.json
├── KernelFactory.cs      — AgentServices: wires IChatClient, IEmbeddingGenerator, QdrantClient
├── RagPipeline.cs        — Orchestrator: ingest, query, chat loop (with timing + token stats)
├── RagPlugin.cs          — Vector search: embed query → Qdrant similarity search
├── DocumentIngester.cs   — PDF → chunks → embed → upsert into Qdrant
├── PdfExtractor.cs       — PDF text extraction via PdfPig
├── TextChunker.cs        — Overlapping word-based chunking
├── appsettings.json      — Configuration (localhost, no secrets)
├── docker-compose.yml    — Starts Qdrant container
└── LocalRagSK.csproj     — NuGet dependencies
```

---

## How Microsoft.Extensions.AI Is Used

| MEAI Feature | Where Used |
|---|---|
| `IChatClient` | `RagPipeline` — chat completion via `GetResponseAsync` / `GetStreamingResponseAsync` |
| `IEmbeddingGenerator<string, Embedding<float>>` | `RagPlugin` + `DocumentIngester` — embed queries and chunks |
| `OllamaChatClient` | `AgentServices.CreateChatClient()` — backed by local Ollama |
| `OllamaEmbeddingGenerator` | `AgentServices.CreateEmbeddingGenerator()` — backed by local Ollama |
| `ChatMessage` / `ChatRole` | `RagPipeline` — builds message history for each turn |
| `ChatResponse.Text` | `QueryAsync` — extracts the answer text |
| `ChatResponse.Usage` | `QueryAsync` — reads `InputTokenCount` / `OutputTokenCount` |
| `GetStreamingResponseAsync` | `ChatLoopAsync` — streams tokens to console |
| `ToChatResponse(updates)` | `ChatLoopAsync` — coalesces stream into a `ChatResponse` for usage stats |
| `QdrantClient` | `RagPlugin` + `DocumentIngester` — direct gRPC connection to Qdrant |

---

## Switching Models

Edit `appsettings.json`:

```json
{
  "Ollama": {
    "ChatModel":      "llama3",          // or phi3, gemma2, codellama
    "EmbeddingModel": "nomic-embed-text" // keep this unless you change VectorDimensions
  }
}
```

> **Warning:** If you change `EmbeddingModel`, you **must** also update `Rag:VectorDimensions`
> and recreate the Qdrant collection (delete `qdrant_storage/` and re-ingest).

### Common embedding models and their dimensions

| Model | Dimensions |
|---|---|
| `nomic-embed-text` | 768 |
| `mxbai-embed-large` | 1024 |
| `all-minilm` | 384 |

---

## Configuration Reference

All settings live in `appsettings.json` and can be overridden with environment variables.

| Key | Default | Description |
|---|---|---|
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama server URL |
| `Ollama:ChatModel` | `mistral` | Chat model name |
| `Ollama:EmbeddingModel` | `nomic-embed-text` | Embedding model name |
| `Qdrant:Host` | `localhost` | Qdrant host |
| `Qdrant:Port` | `6334` | Qdrant **gRPC** port (not the REST port 6333) |
| `Qdrant:CollectionName` | `documents` | Vector collection name |
| `Rag:VectorDimensions` | `768` | Must match the embedding model output size |
| `Rag:ChunkSize` | `400` | Words per chunk |
| `Rag:ChunkOverlap` | `50` | Overlapping words between chunks |
| `Rag:TopK` | `3` | Number of chunks retrieved per query |
| `Rag:MinRelevance` | `0.5` | Minimum cosine similarity score (0–1) |
