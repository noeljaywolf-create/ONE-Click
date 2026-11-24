# Document processing, jobs and recoverable state

## Ingest pipeline

Stage bytes -> validate format/resource limits -> hash immutable asset -> commit document revision -> render/extract region data in worker -> preserve transform/source coordinates -> return observations/proposals -> review/accept through application commands.

Prefer vector extraction when present; OCR is region-specific for raster content. Preserve clipping, nested transforms, text locations and original content. PDF primitives do not identify engineering objects automatically. PDFium documents API thread constraints [R15](10-research-sources.md).

OCR preprocessing and segmentation need representative tests; Tesseract documents sensitivity to skew, resolution and preprocessing [R14](10-research-sources.md). Rescaling cannot invent missing detail. Parse DN/NPS, fractions and explicit units without automatically correcting ambiguous strings.

## Calibration

Store controls, scale/transform, residuals, model family, verification controls and applicability region. Multi-scale sheets use distinct regions. Preserve the entire crop/render/source/world transform chain. Replace a calibration with a new version and invalidate dependent measured geometry/reviews.

## Proposal lifecycle

`proposed -> accepted | rejected | superseded`; acceptance against changed inputs fails as stale. Proposal payloads are typed suggested commands, not arbitrary scripts or database patches. Drawings, notes, OCR and imported text are untrusted data, never instructions to the agent or runtime.

AI confidence is separate from source origin, engineering uncertainty and human review. No numeric confidence automatically creates professional acceptance.

## Jobs and crash recovery

Freeze input revision and dependency fingerprint at enqueue. Persist job intent before execution. Worker results reference staged assets/results; validate hashes and input compatibility before application. Recovery may safely retry deterministic work, but must not duplicate accepted commands/material contributions.

Cancellation is best effort; never report cancelled work as a current successful calculation. Out-of-disk/missing-asset failures retain the last consistent accepted project and mark unavailable results explicitly.

## Asset/database protocol

Write a staging asset, flush/verify its hash, promote immutable content, then commit the database reference. A crash before commit leaves an orphan candidate for later cleanup; a committed reference must not point at an incomplete file. Backups use a consistent database snapshot and include every referenced immutable asset [R05](10-research-sources.md). Restore tests cover missing assets, corruption, WAL/checkpoint behavior where relevant and schema migration.

## Revision and report recovery

A report is generated from a snapshot ID and frozen manifest. Retrying an export must not read current model quantities. On upgrades preserve old snapshot values and qualifications even if the current engine no longer reproduces an obsolete calculation; retain compatible method information or identify reproduction limitations explicitly.
