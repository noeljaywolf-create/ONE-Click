# Acceptance Case Tracker (AC-001..AC-020)

Source: `sprinkler-agent-kit/docs/14-qualification-and-release.md` mandatory initial case catalogue.

**Status meaning is strict.** `synthetic_tested` means executable synthetic/fixture-based check only. It is not independently reviewed, not real-project validated, not manufacturer qualified, and not NFPA certified/compliant.

| ID | Stimulus | Required observation | Status | Evidence level | Evidence refs | Limitations |
|---|---|---|---|---|---|---|
| AC-001 | Segment displacement (3,4,12) m | 13 m route; independent arithmetic | synthetic_tested | Q1 | `tests/Reference/GeometryFormulaTests.cs#G01_IndependentCase_Displacement345_Returns13Meters` | Synthetic unit test only; no independent review or real-project validation. |
| AC-002 | Same XY route, one Z absent | 3D length unknown; 2D projection separately labelled if shown | synthetic_tested | Q1 | `tests/Reference/GeometryFormulaTests.cs#G01_UnknownElevation_ReturnsUnknownSpatialLength` | Synthetic unit test only; no independent review or real-project validation. |
| AC-003 | Two crossing lines at different Z | No joint or fitting added | not_started | Q0 | - | No executable evidence recorded. |
| AC-004 | Collinear/near-collinear, parallel and degenerate geometry | Declared stable outcomes; no divide-by-zero or topology guessed from display | not_started | Q0 | - | No executable evidence recorded. |
| AC-005 | Translate/rotate a known network | Lengths, counts and valid connections unchanged within method policy | not_started | Q0 | - | No executable evidence recorded. |
| AC-006 | Conflicting dimension and calibration evidence | Conflict visible; no silent preferred value | not_started | Q0 | - | No executable evidence recorded. |
| AC-007 | Artificial 6 m span, offsets .153 m and .103 m | 5.744 m cut under that stated synthetic convention | not_started | Q0 | - | No executable application test recorded; kit arithmetic check is not application/product evidence. |
| AC-008 | Offsets consume all available span | Infeasible, not zero/negative purchased pipe | not_started | Q0 | - | No executable evidence recorded. |
| AC-009 | OGS/IGS or other mismatched interface | No automatic compatible-joint assertion; use exact selected profiles | not_started | Q0 | - | No executable evidence recorded; no manufacturer qualification. |
| AC-010 | Unknown head temperature/finish | Head class count can remain; exact purchase variant unresolved | not_started | Q0 | - | No executable evidence recorded; no manufacturer qualification. |
| AC-011 | Bundle plus exploded members | Single declared ownership; no double purchase | not_started | Q0 | - | No executable evidence recorded. |
| AC-012 | Unknown drop in included scope | Known subtotal retained; complete total null with issue | not_started | Q0 | - | No executable evidence recorded. |
| AC-013 | Repeated accepted command / stale command | One effect for replay; stale new mutation rejected | not_started | Q0 | - | No executable evidence recorded. |
| AC-014 | Source/catalogue input changes during calculation | Old result retained with old fingerprint; not installed as current | not_started | Q0 | - | No executable evidence recorded. |
| AC-015 | Crash between asset promotion and DB commit | No committed missing source; documented orphan reconciliation | not_started | Q0 | - | No executable evidence recorded. |
| AC-016 | Backup/restore, then re-export snapshot | Frozen contributions/versions preserved; agreed canonical manifest and quantities agree | not_started | Q0 | - | No executable evidence recorded. |
| AC-017 | Three 4 m cuts from 6 m stock | At least three sticks without splicing; length-only lower bound is not feasible packing | not_started | Q0 | - | No executable evidence recorded. |
| AC-018 | Report or CSV includes unknowns/provisionals | No conversion to zeros, no hidden exclusions, same scope as snapshot | not_started | Q0 | - | No executable evidence recorded. |
| AC-019 | New document revision omits earlier feature | Change classification remains unresolved until reviewed; not automatic deletion | not_started | Q0 | - | No executable evidence recorded. |
| AC-020 | Applicable check has missing criteria or is unimplemented | cannot_verify or unsupported respectively; not pass | not_started | Q0 | - | No executable evidence recorded; no NFPA certification/compliance claim. |

## Summary

| Status | Count |
|---|---:|
| synthetic_tested | 2 |
| not_started | 18 |
| reference_verified | 0 |
| product_qualified | 0 |
| workflow_validated | 0 |
| purpose_released | 0 |
| blocked | 0 |

This summary is an evidence-status summary, not a release or accuracy claim.
