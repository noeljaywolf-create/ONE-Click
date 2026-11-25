# Qualification, benchmark and release protocol

Proposed engineering assurance process, 2026-11-25. It governs claims about the tool; it does not replace project-specific engineering review.

## Evidence ladder

| Level | Claim allowed | Evidence required |
|---|---|---|
| Q0 Specified | Method/support scope is documented | Inputs, outputs, units, signs, limitations and planned cases |
| Q1 Implemented | Code exists and runs in the declared environment | Build and executable case outputs; no real-project accuracy claim |
| Q2 Reference-verified | Method agrees with independent reference cases within declared limits | Analytical/adversarial cases, independent expected results, versioned report |
| Q3 Product-qualified | Named product/interface envelope is qualified | Reviewed manufacturer sources, dimensional semantics, compatibility, assembly and negative cases |
| Q4 Workflow-validated | Declared workflow performs against an independent project benchmark | Frozen documents/truth, held-out projects, predeclared targets and measured results |
| Q5 Purpose-released | Named version may be used within an explicit envelope and purpose | Q-levels required for that purpose, resolved release assessment, authorized decision and known limitations |

Each method/capability can be at a different level. Q2 geometry does not make the whole application Q4. No agent may invent a reviewer, claim accreditation or describe this ladder as an external certification standard.

## Formula qualification record

Every formula/algorithm record must contain: ID/version; authoritative reference or derivation; applicable object and reference geometry; equations and parameter definitions; dimensions/units/signs; finite domain and singularities; unknown/provisional handling; numerical strategy and error criteria; independent expected cases; adversarial cases; evidence owner and reviewer; dependency invalidation. For later hydraulics include boundary conditions and mass/energy residual checks [R12].

Keep golden expected values separate from the implementation being tested. Do not create both with the same production routine. A change in precision, predicate library or compiler settings can require relevant requalification, even when the public API is unchanged.

## Mandatory initial case catalogue

| Case ID | Stimulus | Required observation |
|---|---|---|
| AC-001 | Segment displacement (3,4,12) m | 13 m route; independent arithmetic |
| AC-002 | Same XY route, one Z absent | 3D length unknown; 2D projection separately labelled if shown |
| AC-003 | Two crossing lines at different Z | No joint or fitting added |
| AC-004 | Collinear/near-collinear, parallel and degenerate geometry | Declared stable outcomes; no divide-by-zero or topology guessed from display |
| AC-005 | Translate/rotate a known network | Lengths, counts and valid connections unchanged within method policy |
| AC-006 | Conflicting dimension and calibration evidence | Conflict visible; no silent preferred value |
| AC-007 | Artificial 6 m span, offsets .153 m and .103 m | 5.744 m cut under that stated synthetic convention |
| AC-008 | Offsets consume all available span | Infeasible, not zero/negative purchased pipe |
| AC-009 | OGS/IGS or other mismatched interface | No automatic compatible-joint assertion; use exact selected profiles |
| AC-010 | Unknown head temperature/finish | Head class count can remain; exact purchase variant unresolved |
| AC-011 | Bundle plus exploded members | Single declared ownership; no double purchase |
| AC-012 | Unknown drop in included scope | Known subtotal retained; complete total null with issue |
| AC-013 | Repeated accepted command / stale command | One effect for replay; stale new mutation rejected |
| AC-014 | Source/catalogue input changes during calculation | Old result retained with old fingerprint; not installed as current |
| AC-015 | Crash between asset promotion and DB commit | No committed missing source; documented orphan reconciliation |
| AC-016 | Backup/restore, then re-export snapshot | Frozen contributions/versions preserved; agreed canonical manifest and quantities agree |
| AC-017 | Three 4 m cuts from 6 m stock | At least three sticks without splicing; length-only lower bound is not feasible packing |
| AC-018 | Report or CSV includes unknowns/provisionals | No conversion to zeros, no hidden exclusions, same scope as snapshot |
| AC-019 | New document revision omits earlier feature | Change classification remains unresolved until reviewed; not automatic deletion |
| AC-020 | Applicable check has missing criteria or is unimplemented | cannot_verify or unsupported respectively; not pass |

These are required future acceptance cases, not claims that a production engine passed them. The copied fixture and package checker prove only the narrow arithmetic documented in CHECKS.md.

## Independent project benchmark

Freeze input drawings, revision, scope, selected catalogue and estimating/fabrication conventions before evaluation. Independent reviewers produce object-level reference quantities and record their own disagreements and source limitations. Resolve disagreements or mark uncertain reference items; a spreadsheet labelled 'ground truth' is not automatically correct.

Separate development/tuning projects from held-out projects, including repeated sheets/templates that could leak the same layout into both sets. Stratify by document quality, scale variation, geometry, joining family and ambiguity. Do not assert a meaningful universal sample count without the intended claim and statistical plan.

Report at least:

- Missed/extra objects by class, with matched-object rules declared before scoring.
- Route and cut-length discrepancies per object and per compatible group; aggregate signed agreement alone can hide cancelling errors.
- Wrong sizes/materials/variants and connection/assembly ownership errors.
- Included unknown and provisional scope, including recognition omissions found during review.
- Time to first result, correction/review time and unresolved work remaining.
- Failures/crashes and restoration results under the declared hardware/project envelope.

Recognition precision/recall and length-error distributions measure different behaviours. A confidence score is not an accuracy percentage. If target limits are not agreed before the pilot, report exploratory results only; do not retrofit a passing release gate.

## Purpose-specific release

**Reviewed estimating snapshot:** may include labelled estimates and exclusions under an explicit policy. Its total must not masquerade as a complete exact fabrication/purchase quantity.

**Fabrication/cut schedule:** requires qualified product/end conventions and sufficient geometry for the released pieces. Unresolved fit, cut lengths or interfaces block those affected pieces from fabrication release.

**Procurement schedule:** requires resolvable purchased identity, unique ownership and declared pack/substitution policies. A known head count may coexist with an unreleased SKU schedule.

**Hydraulic or standards finding:** a separate later release purpose with applicability, input and professional-review requirements. Takeoff release does not authorize design compliance claims.

The server evaluates the scope, purpose, role, current dependencies, capability qualification and unresolved-item policy. A reviewer can accept a documented assumption for an estimating purpose; that cannot transform it into an actual measured dimension. Scoped release must list excluded/unreleased scope explicitly.

## Agent gate checklist

Before handing off, attach ENG IDs, capability profile, formula versions, source locators, implemented AC cases with actual results, unimplemented cases, negative-case behaviour and any contract changes. QA verifies the stated evidence level. Domain reviewer authorization is required only for actual engineering qualification/release; continue unaffected development without treating every draft change as an approval request.

## Quality-attribute scenarios

| Trigger / environment | Required response | Measurable acceptance |
|---|---|---|
| Same frozen inputs, supported methods | Reproducible contributions and qualification | Same stable IDs/ownership and quantities within pinned method limits; volatile export metadata excluded from byte comparison |
| User edits an input while job runs | Keep accepted model consistent | No stale output replaces current quantity; demonstrate event-order cases |
| Power/process interruption at write boundaries | Recover committed state and source references | Fault-injection outcomes and restored manifest; recovery-time target still open |
| Unsupported fitting or geometry in included scope | Preserve object and explain limitation | Explicit issue and incomplete affected ledger; no silent omission |
| Large project on declared hardware | Usable load/edit/calculate/export | Measure latency/memory against targets selected before capacity qualification |

These scenarios define what to measure without inventing unresearched production performance or installation tolerances.
