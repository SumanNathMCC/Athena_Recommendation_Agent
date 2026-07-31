# Athena — AI Research Librarian

Grounded PDF RAG pipeline with an embedded document recommender, built on **Semantic Kernel for .NET** (C# 12 / .NET 8).

Athena answers factual questions from a prescribed research corpus with citation-bearing responses, and recommends related documents via a separate document-level retrieval regime. Both share one ingestion pipeline and one Kernel; `ChatCompletionAgent` with `FunctionChoiceBehavior.Auto()` routes between Search and Recommend plugins.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Azure AI services:
  - **Azure OpenAI / Azure AI Foundry** — chat + embeddings
  - **Azure AI Document Intelligence** — layout extraction (tables + prose)
- No API keys in the repository (user-secrets only)

---

## Setup

```bash
cd Athena_Recommendation_Engine
dotnet restore
dotnet build Athena.sln
```

### User secrets (Web project)

```bash
dotnet user-secrets init --project src/Athena.Web

dotnet user-secrets set "AzureFoundry:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/" --project src/Athena.Web
dotnet user-secrets set "AzureFoundry:ApiKey" "YOUR_KEY" --project src/Athena.Web
dotnet user-secrets set "AzureFoundry:ChatDeployment" "gpt-4.1" --project src/Athena.Web
dotnet user-secrets set "AzureFoundry:EmbeddingDeployment" "text-embedding-3-small" --project src/Athena.Web
dotnet user-secrets set "AzureFoundry:EmbeddingDimensions" "1536" --project src/Athena.Web

dotnet user-secrets set "DocumentIntelligence:Endpoint" "https://YOUR-DI.cognitiveservices.azure.com/" --project src/Athena.Web
dotnet user-secrets set "DocumentIntelligence:ApiKey" "YOUR_DI_KEY" --project src/Athena.Web
dotnet user-secrets set "DocumentIntelligence:ModelId" "prebuilt-layout" --project src/Athena.Web
```

The same `AzureFoundry` / `DocumentIntelligence` sections can be set for the ingestion console if you run it outside the Web host (see `src/Athena.Ingestion`).

### Fetch the corpus (PDFs are not committed)

```bash
dotnet run --project src/Athena.Ingestion -- fetch
```

Documents, URLs, licences, and lineage groups are listed in [`corpus/SOURCES.md`](corpus/SOURCES.md) and [`corpus/manifest.json`](corpus/manifest.json).

### Run the Blazor UI

```bash
dotnet run --project src/Athena.Web
```

1. Open **Documents** (Corpus setup).
2. Select documents → **Fetch** → **Extract** → **Inject** (or run the full pipeline).
3. Return to **Chat** and ask grounded questions / recommendation prompts.

Vectors live in an **in-memory** store for this submission: re-inject after an app restart.

### Tests

```bash
dotnet test tests/Athena.Tests/Athena.Tests.csproj
```

Unit coverage includes RRF, chunking windows, citation validation, document-vector strategies, MMR, lineage near-duplicate resolution, and interest-profile decay.

---

## Solution layout

```
Athena.sln
Directory.Packages.props          # centrally pinned package versions
corpus/
  SOURCES.md
  manifest.json
src/
  Athena.Core/                    # records, corpus abstractions (no SK)
  Athena.Ingestion/               # fetch, Document Intelligence extract, chunk, embed, inject
  Athena.Retrieval/               # dense, Lucene BM25, RRF, LLM rerank
  Athena.Recommendation/          # MMR, dedup, blended scoring, interest profile
  Athena.Plugins/                 # thin SearchPlugin + RecommendPlugin
  Athena.Filters/                 # GroundingGuardFilter (+ citation violation log)
  Athena.Agent/                   # ChatCompletionAgent factory + prompt
  Athena.Eval/                    # evaluation harness (stub — see Known gaps)
  Athena.Web/                     # Blazor Server UI
tests/
  Athena.Tests/
```

Plugins stay thin: `[KernelFunction]` attributes, argument shaping, and serialisation only. MMR / RRF / dedup arithmetic live in testable libraries.

---

## Architecture (short)

```
PDFs → Fetch → Document Intelligence (prebuilt-layout)
    → Chunk (FixedWindow | SectionAware)
    → LLM summary/topics → Doc embedding strategy
    → Chunk collection + Doc collection (in-memory VectorData)
              ↓
    Lucene index synced on inject (BM25)
              ↓
    ┌─ SearchPlugin ── hybrid_search / answer_question  (chunk-level)
    └─ RecommendPlugin ─ more_like_this / recommend_for_query / recommend_for_user  (doc-level)
              ↓
    ChatCompletionAgent + FunctionChoiceBehavior.Auto()
              ↓
    Blazor chat (streaming) + Sources sidebar
```

---

## Design decisions (assignment-flagged)

### Extraction stack (deviation from PdfPig / Tesseract)

The brief sketches PdfPig + Docnet/Tesseract hybrid OCR. This submission uses **Azure Document Intelligence `prebuilt-layout`** instead:

- One call yields prose structure **and** tables as first-class layout objects (tables are serialised to Markdown, not whitespace soup).
- Scanned/low-text pages are handled by the service’s OCR path without a separate local Tesseract install.
- **Cost of this choice:** dependency on a cloud DI resource; the manufactured `A1-scanned.pdf` OCR-delta diagnostic from §4.1 is not yet produced in-repo (see Known gaps). Where the brief requires PdfPig word boxes for custom heading heuristics, we instead rely on DI’s section/heading signals in the extracted Markdown.

### Chunker choice

Both chunkers are implemented:

| Chunker | Behaviour |
|---------|-----------|
| `FixedWindowChunker` | ~**800** tokens, **20%** overlap; tables kept atomic |
| `SectionAwareChunker` | Split on headings; subdivide sections over the same max window with the same overlap |

**Default:** `SectionAware` (`Chunking:Strategy` in `appsettings.json`).

**Why:** Cluster A (numbered regulatory principles) and clusters B/C (academic papers) have usable heading structure in DI Markdown; section boundaries reduce mid-principle splits that hurt citation precision. Fixed-window remains available for ablation and for documents where headings are noisy.

Overlap is **20%** (brief suggests ~15% for fixed-window). The higher overlap was chosen to reduce boundary losses on short identifier queries (e.g. “d516 Principle 6”) after observing long reference chunks; final pick should be confirmed with Part F Context Recall (not yet run — see Known gaps).

### Fusion strategy

Hybrid retrieval:

1. Dense top-20 (chunk embeddings, cosine)
2. Lexical top-20 (Lucene BM25 over the same chunks)
3. **Reciprocal Rank Fusion** with \(k = 60\) → top-10
4. LLM rerank (0–10 relevance) → top-\(K\) (default 6)

**Why RRF, not score blending:** cosine similarity and BM25 live on incomparable scales. Averaging or naive min-max blends invents an ordering; RRF uses ranks only. Lexical search is kept deliberately: short exact identifiers and principle numbers in this corpus are where BM25 earns its keep.

### Document-vector strategy

All three strategies are implemented:

| Strategy | Construction |
|----------|--------------|
| `CentroidStrategy` | Mean of chunk embeddings |
| `SummaryStrategy` | Embed the ≤150-word LLM summary |
| `CompositeStrategy` | Embed `Title + Topics + Summary` |

**Default:** `Summary` (`DocumentVector:Strategy`).

**Why not centroid as default:** long multi-topic documents (e.g. B3 RAG survey) produce a centroid near the “middle” of the corpus and recommend poorly. Short documents (e.g. A5 FSI summary) are less harmed by centroids, which is exactly the length asymmetry the brief asks us to notice. Summary (and composite) embeddings stay topic-sharp for recommendation. Centroid vs summary must still be ablated on nDCG@5 in Part F.

### Near-duplicate resolution (Part D.2)

**Method:** `LineageNearDuplicateResolver`

1. **Primary — `LineageGroup` metadata** (from `manifest.json`): within each lineage group keep only the **newest** `PublishedOn` (final over draft for A1/A2, A3/A4, C4/C5). Other members of the **seed’s** lineage never surface beside the seed in `more_like_this`.
2. **Secondary — cosine ceiling 0.97:** collapses unlabeled near-twins that slip past metadata.

**Cost named (as required):**

- Lineage metadata is **accurate** for known draft/final pairs but **not general** — a fourth unlabeled pair added tomorrow would not be collapsed until annotated (or until it exceeds the cosine ceiling).
- The similarity ceiling **generalises** but can suppress two genuinely distinct documents that happen to be written similarly.
- **Not used:** hardcoded filename / doc-id exclusion lists.

### Recency prior (\(\tau\))

In `recommend_for_query` scoring:

\[
\text{recency} = \exp(-\text{ageDays} / \tau),\quad \tau = 730 \text{ days (2 years)}
\]

Final blend (defaults):

\[
0.50 \cdot \text{docSim} + 0.35 \cdot \text{normalisedChunkAggregate} + 0.15 \cdot \text{recency}
\]

Chunk aggregates use reciprocal rank of hybrid hits, divided by \(\log(1 + \text{pageCount})\) to damp length bias, then min-max normalised.

**Why \(\tau = 730\):** the corpus spans ~2020–2025. A two-year scale prefers fresher notes without erasing foundational papers (B1/B2). A1 vs A2 are only seven months apart — recency alone does not separate them; lineage dedup does. Weights are fixed and intended for Part F ablation.

### MMR \(\lambda\)

Default \(\lambda = 0.7\). `more_like_this` exposes \(\lambda\) to the model. Pure relevance \(\lambda = 1.0\) collapses diversity; \(\lambda = 0.3\) trades nDCG for intra-list diversity — report both in Part F / demo for seed **B3**.

### Interest profile decay (Part D.9.4 question)

Update rule (scoped per Blazor circuit / session — **not** a process-wide singleton):

\[
\text{profile} \leftarrow 0.8 \cdot \text{profile} + 0.2 \cdot \text{embed}(\text{latestQuery})
\]

Surfaced doc ids are excluded on later `recommend_for_user` calls. Profile is also updated from Search (`answer_question` / `hybrid_search`) so factual turns feed recommendations.

**Analyst spends ~20 turns on cluster A, then switches to C — with decay 0.8, how long until the profile is “about C”?**

Residual weight of the old topic after \(n\) new (cluster-C) updates is \(0.8^n\). For the old mass to drop below ~5%: \(0.8^n < 0.05 \Rightarrow n \approx 14\) turns. After only a few C turns the profile is still a **mixture** pointing between A and C — i.e. about **neither** cluster cleanly. That is the failure mode of an exponentially weighted mean of unrelated topics.

**What we do about it:** (1) session-scoped profile (no cross-user bleed); (2) already-surfaced exclusion so the UI does not repeat the same A docs forever; (3) MMR diversification on the profile vector. We do **not** maintain dual centroids or hard cluster switching in this submission — that trade-off is accepted and should be called out in the demo if crossover recommendations look muddled.

### Grounding (Part C)

- `prompts/answer.yaml`: every factual sentence needs `[Title, p.N]`; otherwise exactly `INSUFFICIENT_CONTEXT`.
- `GroundingGuardFilter` (`IFunctionInvocationFilter`) validates citations against the retrieved passage set for `answer_question`, strips/fails unsupported ones, and appends to `logs/citation-violations.jsonl`.
- Agent instructions: out-of-scope trivia → respond with exactly `INSUFFICIENT_CONTEXT` (no tool call / no apology essay).

### Agent routing (Part E)

Six plugin functions, selected only via `[Description]` text + system instructions — **no** `if (query.Contains("recommend"))` intent matching.

| Utterance (brief) | Expected tool |
|-------------------|---------------|
| d516 tolerance for disruption | `answer_question` |
| What else should I read? | `recommend_for_user` |
| Papers like RAPTOR | `more_like_this` |
| What should I read about evaluating RAG? | `recommend_for_query` |
| Summarise graph RAG vs vanilla + further reading | QA + recommend |
| 2019 Cricket World Cup | `INSUFFICIENT_CONTEXT`, no tools |

---

## Configuration reference

| Section | Key defaults |
|---------|----------------|
| `Chunking` | `MaxTokens=800`, `OverlapRatio=0.20`, `Strategy=SectionAware` |
| `DocumentVector` | `Strategy=Summary` |
| `Recommendation` | weights 0.50 / 0.35 / 0.15, `RecencyTauDays=730`, `NearDuplicateCeiling=0.97`, `DefaultLambda=0.7` |
| `Agent` | `PromptFile=Agents/AthenaResearchLibrarian.md` |

---

## Package pinning

Versions are centralised in [`Directory.Packages.props`](Directory.Packages.props). Notable pins:

- `Microsoft.SemanticKernel` **1.51.0** (+ Agents.Core, Azure OpenAI, InMemory connector)
- `Microsoft.Extensions.VectorData.Abstractions` **9.0.0-preview.1.25229.1**
- `Azure.AI.DocumentIntelligence` **1.0.0**
- `Lucene.Net` **4.8.0-beta00017**

Record attribute names follow the VectorData surface shipped with these packages (`VectorStoreRecordKey` / `VectorStoreRecordData` / `VectorStoreRecordVector`).

---

## UI notes

- Blazor Server, streaming chat via `InvokeStreamingAsync`
- Circuit-scoped `ChatUiState` retains chat + **Sources** sidebar across navigation to Documents
- **Clear chat** resets UI state, Sources, and the agent thread
- Right sidebar title: **Sources** (retrieved passages after hybrid + rerank)

---

## Known gaps (honest status vs full rubric)

| Area | Status |
|------|--------|
| Part A — `A1-scanned.pdf` + OCR delta metric | Not manufactured yet |
| Part F — EvalHarness, 25 QA / 8 rec gold cases, CSV ablations | `Athena.Eval` still a stub; design choices above anticipate the ablations |
| Part G — `TelemetryFilter`, `PiiRedactionFilter`, clickable citation panel, persistent **Recommended for you** sidebar | Partial (streaming chat + Sources; recommend tools work in-chat) |
| `REPORT.md` + demo video | Separate deliverables |

These do not change the design decisions above; they are listed so the README is not mistaken for a claim of full rubric completion.

---

## Licence / corpus

Prescribed and learner-sourced documents are public-sector or open-access materials. See [`corpus/SOURCES.md`](corpus/SOURCES.md) for per-document URL, retrieval date, and licence. Do not commit PDFs.
