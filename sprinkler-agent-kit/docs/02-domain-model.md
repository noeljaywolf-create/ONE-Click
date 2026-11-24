# Domain, state and data contracts

## Units and coordinate frames

Internal spatial coordinates: metres in a right-handed frame, +Z upward, +X/+Y defined by project grids. Radians internally. Gauge pressure: Pa; hydraulic flow: m3/s; mass: kg. Display units are independent and retain original entry units. JSON numbers must be finite. Do not round engineering state for display.

`NominalSize(system, designation)` is distinct from `OutsideDiameterM` and `InsideDiameterM`. Catalogue values may themselves be nominal dimensions; preserve that qualification.

## Entities

| Entity | Required identity/context |
|---|---|
| Project | ID, integer revision, datum/frame, conventions, systems, included/excluded scope |
| DocumentRevision | ID, asset hash, issue metadata, supersedes relation, processing state |
| DrawingRegion | ID, document revision, page, bounds, transform chain, calibration evidence |
| Observation/Claim | ID, property path, candidate value, source region/cell, origin, derivation |
| Route | ID, ordered geometry, nominal spec, source refs, system/zone/work status |
| PipePiece | ID, route ancestry, physical cut-end planes, preparation, assembly references |
| Component | ID, pinned catalogue item/release, local-to-world pose, work status |
| Port | ID, owner, position, unit outward normal, interface specification, optional clocking |
| Joint | ID, confirmed ports, selected joining adapter and assembly ownership |
| Assembly | ID, members, candidate/accepted state, exact purchased contents |
| ReviewDecision | ID, subject/property, purpose, input fingerprint, actor attribution, outcome |
| QuantityContribution | ID, owner, ledger, group key, qualified value, source/method versions |
| Job | ID, kind, frozen input refs, lifecycle, progress, output refs, failure details |
| TakeoffSnapshot | ID, project/model input revision, result ID, policy/versions, exclusions; release decisions stored separately |

`route`, `fabrication`, `hydraulic`, `procurement` are distinct ledger identifiers. Numeric totals with different conventions may not be summed even if both use metres.

## Tagged values

Use a closed union: `known` with numeric `value`; `provisional` with numeric `value`, assumption refs and issue refs; `unknown` with `value:null`, reason and issue refs. Every variant carries `unit`, `sourceRefs` and `methodVersion`. Unknown is not represented by an absent field, zero or NaN. Known describes available calculation input/output, not professional approval.

Origins: `document_stated`, `measured`, `imported`, `inferred`, `assumed`, `manual`. Review states: `unreviewed`, `accepted`, `rejected`, `clarification_required`, `stale`. Acceptance does not change origin. A manual value needs an origin note even without a drawing source.

Check outcomes: `pass`, `fail`, `cannot_verify`, `not_applicable`, `unsupported`. Freshness: `current`, `stale`, `failed`. Completeness and review purpose are separate from these fields.

## Aggregate semantics

Keep known and provisional subtotals separate; `completeTotal:null` if included scope has unknown contributions. A finite uncertainty interval is allowed only with justified bounds and provenance. It cannot stand in for an unknown assembly or unbounded hidden routing. Never sum incompatible SKU/spec/size/joining/work-status groups.

## Revision ownership

Project revision increases on accepted domain, source-reference, policy, catalogue-selection and review mutations. Job progress and disposable caches do not advance it. Derived result insertion references a frozen input revision and does not itself revise accepted state. A snapshot freezes result and review state; release creates a separate immutable decision at a later current project revision.

An imported file is immutable. A new file creates a new document revision. If source bytes change, the asset hash changes. Split/merge ancestry must remain navigable.

## Persistence

Store structured accepted state in an embedded transactional database and sources in immutable content-addressed storage. Stage+hash+flush assets before committing references; garbage-collect unreferenced staging only through a documented retention policy. Use the database backup API or equivalent consistent snapshot mechanism, then package referenced assets and verify restore [R05](10-research-sources.md). Do not assume copying a live database and asset directory is one atomic operation.

Local actor names are attribution, not authenticated professional signatures. Authenticated signing/tamper evidence is an open decision. Never fabricate reviewer authority.
