# Internal APIs and optional HTTP adapter

## Contract priority

The engineering API is in-process and transport-neutral. `contracts/openapi.json` describes an optional HTTP adapter using OpenAPI 3.1.1 (a deliberate pinned version, not a claim it is latest) [R01](10-research-sources.md). Read this document for semantics that schema validation cannot express.

## Internal signatures

```text
ExecuteCommand(projectId, expectedRevision, commandId, command, actorContext)
  -> CommandReceipt(newRevision, createdIds, invalidatedIds, issueIds)
QueryProject(projectId) -> ProjectView(revision, capabilities, scope)
Geometry.Length(immutableGeometry, tolerancePolicy) -> QualifiedLength
Topology.ValidateConnection(portA, portB, context) -> CandidateResult
Assemblies.Resolve(frozenNetwork, pinnedCatalogue, conventions) -> ResolutionResult
Quantities.Calculate(frozenModel, ledger, scope, conventions) -> CalculationResult
Snapshots.Freeze(resultId, frozenReviewContext, exclusions) -> Snapshot
```

Actor context comes from the session, not a caller-supplied display name treated as authorization. Pure core calls receive every numerical and catalogue input explicitly.

## Transport operations

| Path | Method | Purpose |
|---|---|---|
| `/projects` | POST | Create project; no existing project revision |
| `/projects/{projectId}` | GET | Accepted-state summary and strong ETag |
| `/projects/{projectId}/uploads` | POST | Stage PDF bytes; return server-owned handle |
| `/projects/{projectId}/commands` | POST | One atomic typed command |
| `/projects/{projectId}/jobs` | POST | Start extraction/assembly/quantity/export job on pinned inputs |
| `/projects/{projectId}/jobs/{jobId}` | GET | Poll state, output IDs and input fingerprint |
| `/projects/{projectId}/jobs/{jobId}/cancellation` | POST | Request cancellation |
| `/projects/{projectId}/results/{resultId}` | GET | Qualified derived quantities and issues |
| `/projects/{projectId}/snapshots` | POST | Freeze a compatible result and review context |
| `/projects/{projectId}/snapshots/{snapshotId}` | GET | Immutable snapshot manifest |

This is the first-slice contract, not an exhaustive catalogue-admin/BIM/hydraulic API. Unsupported operations return explicit errors rather than accepting unused fields.

The orchestration schema also intentionally omits detailed region/evidence/proposal/model queries and artifact streaming. Add those typed contracts at the corresponding delivery gate before implementing dependent clients; see [coverage limits](../CHECKS.md). Import processing must create region identities before calibration. Manual entry records a claim from its origin note. Proposal IDs alone are not enough for a review UI: retrieval must expose the typed proposed changes, evidence and input fingerprint before acceptance.

## Typed commands

Initial union: `ImportDocument`, `SetCalibration`, `UpsertRoute`, `PlaceComponent`, `ConnectPorts`, `AcceptProposal`, `ReviewProperty`, `SelectAssembly`, `ReleaseSnapshot`. No arbitrary table updates or unrestricted AI patches. Referenced IDs must belong to the project and exact expected revisions. `UpsertRoute` atomically validates and creates/updates derived route-end port identities; referenced existing ports cannot be silently orphaned. Component ports come from pinned catalogue definitions.

Command examples in the machine contract are structural examples; they do not imply referenced resources exist or are reviewed.

## Concurrency and replay

GET project returns a strong entity tag such as `"project:p-demo:r7"`. Accepted-state mutation POSTs require `If-Match` with that project representation's exact tag. Missing precondition -> 428; mismatched revision -> 412. RFC 9110 defines conditional-request semantics [R02](10-research-sources.md); mandatory project-level preconditions and 428 are this application's policy.

Every POST accepts `X-Operation-Id`. This is an application-defined replay key, not a claimed IETF idempotency standard. Key scope is actor/session, project, method and resource path. Persist the canonical body hash and logical result for the operation lifetime/project history. Same key+same body returns the original receipt without reapplying; same key+different body -> 409. A replay already successfully committed may return its original receipt even if project revision advanced; a never-committed stale command fails. Do not evict mutation keys while retry/recovery can replay them.

For uploads hash the exact byte stream and relevant metadata rather than canonical JSON. Verify authentication and replay-key scope before looking up a stored result. Cancellation also requires the current project precondition but never changes accepted engineering state. A repeated cancellation must reconcile a job that already finished.

Job-start and staging requests pin current project inputs with `If-Match` but do not modify accepted state. A job's derived result remains linked to its old revision; it is not eligible as current after dependencies change. Snapshot creation requires matching current inputs and advances project revision once. Snapshot release is a separate current-revision command.

Export jobs depend on an immutable snapshot manifest. They may export a historical snapshot after later project edits; identify it as historical rather than invalidating its frozen contents. Result freshness compares actual dependencies, not job progress or unrelated cache changes. A snapshot manifest stays immutable; release decisions are separate audit records, not edits to its quantities.

Schema checks cannot establish project membership, reference existence, positive physical dimensions, noncoincident route vertices, normalized orientation, assembly compatibility, sufficient/nondegenerate calibration controls, valid release authority, or aggregate arithmetic. Enforce these in the application/core. A route vertex's Z is a qualified metre value, so absent elevation can be retained without inventing a 3D length. The first slice does not support every partially unknown geometric representation.

## Job states

`queued -> running -> succeeded | failed | cancelled`; `cancellation_requested` may precede success or cancellation. Cancelling is cooperative and does not guarantee an already-finished result is removed. Recovered queued/running jobs must reconcile input/output ownership before restarting. Progress is informational, never proof of completed calculation.

## Error model

Use `application/problem+json` following RFC 9457 [R03](10-research-sources.md), with `type`, `title`, `status`, `detail`, `instance` and project extensions `code`, `issueIds`, `currentRevision`. Use stable URNs for application problem types; do not invent a public standards URL.

400 malformed payload; 401/403 session authorization; 404 missing or inaccessible project-scoped resource; 409 invariant/replay conflict; 412 stale inputs; 413 configured input limit; 422 structurally valid but impossible geometry/unsupported required convention; 428 required precondition absent. Valid jobs may succeed with qualified unknowns: engineering incompleteness is not always an HTTP exception.

## Resource and release rules

Uploads accept bytes and sanitized metadata, never arbitrary local paths or download URLs. Enforce configurable size/page/pixel/time limits, document exact supported formats and reject nonfinite coordinates before calculation. Default limits require a measured project-capacity decision, not invented production numbers.

Freeze records calculation ID, all source/catalogue/method/policy versions, scope, known/provisional/unknown quantities and exclusions. `ReleaseSnapshot` checks authorized role, compatible purpose and unresolved-item policy. It cannot change a draft's quantities or mark assumptions as facts. Old snapshot releases remain historical, not automatically current for a changed project.
