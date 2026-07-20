# Athena Corpus — Sources

This file lists every document in the Athena research library: prescribed clusters **A–C** (assignment), learner-sourced cluster **D**, and the manufactured OCR diagnostic document.

**Ground rules (assignment §4.2):**

- PDFs are **not** committed to Git. Use `corpus/manifest.json` and `dotnet run --project src/Athena.Ingestion -- fetch` to download them into `corpus/pdfs/`.
- If a link has moved, find the document by title on the publisher site, update `manifest.json`, and record the substitution below.
- Default retrieval date for all URLs verified below: **2026-07-20**.

---

## Cluster A — Operational resilience regulation (BIS / Basel Committee)

| ID | Title | URL | ~pp | Retrieval date | Licence | Notes |
|----|-------|-----|-----|----------------|---------|-------|
| A1 | Principles for Operational Resilience (final, Mar 2021) — BCBS d516 | https://www.bis.org/bcbs/publ/d516.pdf | 20 | 2026-07-20 | BIS public publication ([terms](https://www.bis.org/terms_conditions.htm)) | Final; pairs with A2 |
| A2 | Principles for Operational Resilience (consultative draft, Aug 2020) — BCBS d509 | https://www.bis.org/bcbs/publ/d509.pdf | 18 | 2026-07-20 | BIS public publication ([terms](https://www.bis.org/terms_conditions.htm)) | Draft; pairs with A1 |
| A3 | Revisions to the Principles for the Sound Management of Operational Risk (final, Mar 2021) — BCBS d515 | https://www.bis.org/bcbs/publ/d515.pdf | 30 | 2026-07-20 | BIS public publication ([terms](https://www.bis.org/terms_conditions.htm)) | Final; pairs with A4 |
| A4 | Revisions to the PSMOR (consultative draft, Aug 2020) — BCBS d508 | https://www.bis.org/bcbs/publ/d508.pdf | 30 | 2026-07-20 | BIS public publication ([terms](https://www.bis.org/terms_conditions.htm)) | Draft; pairs with A3 |
| A5 | FSI Executive Summary: Principles for operational resilience | https://www.bis.org/fsi/fsisummaries/op_resilience.pdf | 3 | 2026-07-20 | BIS public publication ([terms](https://www.bis.org/terms_conditions.htm)) | Length outlier (short doc) |

---

## Cluster B — RAG architecture and retrieval (arXiv)

| ID | Title | URL | ~pp | Retrieval date | Licence | Notes |
|----|-------|-----|-----|----------------|---------|-------|
| B1 | Lewis et al., *Retrieval-Augmented Generation for Knowledge-Intensive NLP Tasks* | https://arxiv.org/pdf/2005.11401 | 19 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Foundational RAG paper |
| B2 | Karpukhin et al., *Dense Passage Retrieval for Open-Domain Question Answering* | https://arxiv.org/pdf/2004.04906 | 13 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Dense retrieval |
| B3 | Gao et al., *Retrieval-Augmented Generation for Large Language Models: A Survey* | https://arxiv.org/pdf/2312.10997 | 30+ | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Long multi-topic survey |
| B4 | Sarthi et al., *RAPTOR: Recursive Abstractive Processing for Tree-Organized Retrieval* | https://arxiv.org/pdf/2401.18059 | 24 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Tree-organised retrieval |
| B5 | Edge et al., *From Local to Global: A Graph RAG Approach to Query-Focused Summarization* | https://arxiv.org/pdf/2404.16130 | 16 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Graph RAG |

---

## Cluster C — Graph RAG and evaluation (arXiv)

*Deliberately adjacent to cluster B — tests recommender boundary between retrieval architecture and graph RAG / evaluation.*

| ID | Title | URL | ~pp | Retrieval date | Licence | Notes |
|----|-------|-----|-----|----------------|---------|-------|
| C1 | Peng et al., *Graph Retrieval-Augmented Generation: A Survey* | https://arxiv.org/pdf/2408.08921 | 25 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Graph RAG survey |
| C2 | Guo et al., *LightRAG: Simple and Fast Retrieval-Augmented Generation* | https://arxiv.org/pdf/2410.05779 | 18 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Lightweight graph RAG |
| C3 | Han et al., *Retrieval-Augmented Generation with Graphs (GraphRAG)* | https://arxiv.org/pdf/2501.00309 | 25 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | GraphRAG |
| C4 | Es et al., *RAGAS: Automated Evaluation of Retrieval Augmented Generation* — v1 | https://arxiv.org/pdf/2309.15217v1 | 6 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Pairs with C5 |
| C5 | Es et al., *RAGAS: Automated Evaluation of Retrieval Augmented Generation* — v2 | https://arxiv.org/pdf/2309.15217v2 | 7 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Pairs with C4 |

---

## Cluster D — Agentic AI systems and tool-using language models (learner-sourced)

*Coherent fourth topic chosen for this submission: agentic AI and multi-agent LLM systems, sourced from arXiv. Distinct from clusters B/C (RAG architecture / graph RAG) so the recommender is evaluated on a topic not tuned in the assignment brief.*

| ID | Title | URL | ~pp | Retrieval date | Licence | Notes |
|----|-------|-----|-----|----------------|---------|-------|
| D1 | Yao et al., *ReAct: Synergizing Reasoning and Acting in Language Models* | https://arxiv.org/pdf/2210.03629 | 15 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Reasoning + tool use |
| D2 | Schick et al., *Toolformer: Language Models Can Teach Themselves to Use Tools* | https://arxiv.org/pdf/2302.04761 | 18 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Self-supervised tool learning |
| D3 | Xi et al., *The Rise and Potential of Large Language Model Based Agents: A Survey* | https://arxiv.org/pdf/2309.07864 | 22 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Agent survey |
| D4 | Hong et al., *MetaGPT: Meta Programming for A Multi-Agent Collaborative Framework* | https://arxiv.org/pdf/2308.00352 | 20 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Multi-agent framework |
| D5 | Wu et al., *AutoGen: Enabling Next-Gen LLM Applications via Multi-Agent Conversation* | https://arxiv.org/pdf/2308.08155 | 20 | 2026-07-20 | [arXiv licence](https://info.arxiv.org/help/license/index.html) | Multi-agent orchestration |

---

## Manufactured document — OCR diagnostic (assignment §4.1)

| ID | Title | Source | ~pp | Created | Licence | Notes |
|----|-------|--------|-----|---------|---------|-------|
| A1-scanned | BCBS d516 pages 1–12, rasterized image-only PDF | Derived from A1 (d516) | 12 | 2026-07-20 | Derived from BIS d516; local manufacture only | **Not downloaded.** Generate with Docnet.Core at 200 DPI; ingest under distinct `DocId` alongside text-native A1 |

---

## Version-lineage pairs (near-duplicate trap)

These pairs sit at very high cosine similarity and must be handled by the recommender (`LineageGroup` in `DocRecord`):

| Lineage group | Draft / older | Final / newer |
|---------------|---------------|---------------|
| `bcbs-op-resilience` | A2 (d509, Aug 2020) | A1 (d516, Mar 2021) |
| `bcbs-psmor` | A4 (d508, Aug 2020) | A3 (d515, Mar 2021) |
| `ragas` | C4 (v1) | C5 (v2) |

---

## Corpus summary

| Cluster | Documents | Approx. pages |
|---------|-----------|---------------|
| A | 5 (+ 1 manufactured) | ~101 |
| B | 5 | ~104 |
| C | 5 | ~81 |
| D | 5 | ~95 |
| **Total** | **21 downloadable + 1 manufactured** | **~350–450** |

---

## URL substitutions

*Record any link changes here if a publisher moves a document.*

| Original ID | Original URL | Substitute URL | Date substituted | Reason |
|-------------|--------------|----------------|------------------|--------|
| — | — | — | — | None yet |
