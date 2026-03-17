# Local RAG — Semantic Kernel + Ollama + Qdrant

A fully local RAG pipeline orchestrated by **Semantic Kernel**.  
No Azure. No API keys. No cloud costs.

---

## Stack

| Layer | Local (this project) | Azure equivalent |
|---|---|---|
| **Orchestrator** | Semantic Kernel | Semantic Kernel |
| **Chat LLM** | Ollama / phi | Azure OpenAI / gpt-4o |
| **Embeddings** | Ollama / nomic-embed-text | Azure OpenAI / text-embedding-3-small |
| **Vector store** | Qdrant (Docker) | Azure AI Search |

---

## Prerequisites

### 1. Docker
Used to run Qdrant.

### 2. Ollama
Download from https://ollama.com, then pull the required models:

```bash
ollama pull nomic-embed-text   # embedding model — 768 dimensions
ollama pull phi            # chat model (or llama3, phi3, gemma2, etc.)
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

---

## Project Structure

```
LocalRagSK/
├── Program.cs            — Entry point, command routing
├── AppConfig.cs          — Strongly-typed config from appsettings.json
├── KernelFactory.cs      — Wires SK to Ollama + Qdrant  ← only file that differs from Azure
├── RagPipeline.cs        — Orchestrator: ingest, query, chat loop  [identical to Azure]
├── RagPlugin.cs          — SK Plugin: [KernelFunction] search_documents  [identical to Azure]
├── DocumentIngester.cs   — PDF → chunks → SK memory → Qdrant  [identical to Azure]
├── PdfExtractor.cs       — PDF text extraction via PdfPig  [identical to Azure]
├── TextChunker.cs        — Overlapping word-based chunking  [identical to Azure]
├── appsettings.json      — Configuration (localhost, no secrets)
├── docker-compose.yml    — Starts Qdrant container
└── LocalRagSK.csproj     — NuGet dependencies
```

---

## How Semantic Kernel Is Used

| SK Feature | Where Used |
|---|---|
| `Kernel.CreateBuilder()` | `KernelFactory.CreateKernel()` — registers Ollama services |
| `AddOllamaChatCompletion()` | Chat with mistral/llama3/phi3 locally |
| `AddOllamaTextEmbeddingGeneration()` | Embeds text via nomic-embed-text locally |
| `QdrantMemoryStore` | Local vector store — no cloud needed |
| `SemanticTextMemory` | Combines Qdrant store + Ollama embeddings |
| `memory.SaveInformationAsync()` | `DocumentIngester` — embed + store chunks |
| `memory.SearchAsync()` | `RagPlugin` — retrieve relevant chunks |
| `[KernelFunction]` attribute | `RagPlugin.SearchDocumentsAsync()` — registered as a plugin |
| `CreateFunctionFromPrompt()` | RAG answer prompt template with `{{$context}}` |
| `KernelArguments` | Injects retrieved context into the prompt |
| `ChatHistory` | Tracks conversation for follow-up questions |
| `GetStreamingChatMessageContentsAsync()` | Streams response tokens to console |

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

> ⚠️ If you change `EmbeddingModel`, you **must** also update `Rag:VectorDimensions`  
> and recreate the Qdrant collection (delete `qdrant_storage/` and re-ingest).

### Common embedding models and their dimensions

| Model | Dimensions |
|---|---|
| `nomic-embed-text` | 768 |
| `mxbai-embed-large` | 1024 |
| `all-minilm` | 384 |

---

## Migrating to Azure

To switch this project to Azure services, replace only `KernelFactory.cs`:

```csharp
// Replace Ollama with Azure OpenAI
builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
builder.AddAzureOpenAITextEmbeddingGeneration(deploymentName, endpoint, apiKey);

// Replace Qdrant with Azure AI Search
var memoryStore = new AzureAISearchMemoryStore(indexClient);
```

Every other file — `RagPipeline`, `RagPlugin`, `DocumentIngester`, `TextChunker`, `PdfExtractor` — stays byte-for-byte identical. That's the value of SK's abstraction layer.
