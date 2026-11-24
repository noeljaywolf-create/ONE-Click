# Architecture decision records and open decisions

All recommendations dated 2026-11-24. No organization or professional approval is implied. Owners below are responsibilities to assign, not named assignees.

## ADR-001: modular local desktop core

Status: recommended. Context: Windows-first, offline manual work, one initial project editor. Choose modular in-process engineering with bounded workers. Alternatives: web/cloud-first or distributed microservices. Benefits: simpler state, deployment and reproducibility. Costs: desktop distribution and local recovery ownership. Reopen for actual simultaneous collaboration or measured workload requirements. Validate representative project performance and recovery. Owner: application lead.

## ADR-002: renderer-independent C# core, WPF candidate shell

Status: recommended; runtime and renderer unselected. Alternatives: cross-platform shell/native C++/web-rendered desktop. Benefits: Windows integration and testable .NET domain; cost: platform scope and native CAD integration risk. No CAD SDK is approved by this ADR. Prototype picking, snapping, transforms, large-sheet interaction and licensing. Owner: desktop/core leads. Reopen for cross-platform requirement or unsuitable SDK interoperability [R16](10-research-sources.md).

## ADR-003: internal commands first, optional HTTP adapter

Status: recommended. Alternative: route every edit through a local server. Internal calls reduce deployment overhead; OpenAPI adapter supports tools/automation later. Cost: maintain transport mapping and parity tests. Reopen for process isolation or integration needs. Owner: contracts lead. Verify every adapter mutation uses the same command path; no duplicate business logic.

## ADR-004: immutable sources/catalogues and snapshot-based reporting

Status: recommended. Alternative: editable source blobs/live catalogue lookups. Benefit: explainable historical results; cost: storage and migration planning. Validate backup/restore and catalogue correction impact. Owner: persistence/catalogue leads. Reopen retention policies with traceability requirements intact.

## ADR-005: AI proposals with explicit acceptance

Status: recommended. Alternative: direct AI mutation or fully manual-only product. Benefit: useful assistance with auditable engineering state. Costs: review UX and candidate reconciliation. Validate time-to-reviewed-takeoff and error scope, not only detection scores. Owner: documents/QA leads. Reopen automated acceptance only for narrowly qualified tasks under a documented policy.

## ADR-006: project-level optimistic concurrency and durable replay receipts

Status: recommended. Alternative: object-level merging or last-writer-wins. Project-level revision is simple but creates more conflicts. Last-writer-wins can silently lose accepted decisions. Validate replay/crash and concurrent request cases. Owner: application/contracts. Reopen for demonstrated concurrent-editing needs. HTTP semantics derive from RFC 9110; replay policy is application-defined [R02](10-research-sources.md).

## Unresolved product/engineering decisions

First catalogue/joining family; actual traceable product dimensions; whether fabricated stocklists are first-release output; standards/jurisdiction/edition; curve/flexible support; hardware/project size; numerical, fit and measurement tolerances; real-project accuracy targets; data/model licensing; authenticated review identity; external processing consent/retention.

Record each decision's evidence, alternatives, impacts, assigned owner and revisit trigger. Do not transform an agent's default into a professionally approved criterion.
