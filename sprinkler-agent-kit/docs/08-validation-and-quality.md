# Validation matrix and acceptance claims

## Separate claims

Schema validation checks structure. Mathematical verification checks implementations. Catalogue qualification checks product semantics. Project validation checks reconstructed installations against independent references. Operational testing checks recovery/reproducibility. None alone establishes all the others.

The included reference fixture has artificial net offsets, a 21 m route and 19.77 m cut total. Its 15 historical checks do not validate PDF interpretation or infer physical parts. Preserve that scope in tests and reports.

## Minimum test matrix

| Area | Required positive and negative cases |
|---|---|
| Geometry | Horizontal/vertical/sloped/offset, rotation/translation, reversal, tiny/huge coordinates, degenerate/near-parallel inputs |
| Topology | Connected branch, unconnected crossing, intentional terminal, incompatible interface, transitive proximity-chain rejection |
| Calibration | Known controls, held-out residuals, mixed scales, crop/rotation, bad control, unsupported distortion |
| Assemblies | Catalogue dimensions, signed offsets, contents ownership, alternatives, impossible fit, unsupported angle |
| Quantities | Four-ledger separation, known/provisional subtotals, unknown total, scope exclusions, duplicate ownership |
| API | Stale If-Match, replay same/different body, project isolation, unknown command, invalid units/nonfinite values |
| Jobs | Cancellation race, stale output, interrupted worker, retry after crash, no duplicate application |
| Review | Accepted assumption stays assumed; changes mark relevant reviews stale; release purpose enforced |
| Persistence | Interrupted writes, absent/corrupt assets, restore, migration, immutable snapshots |
| Symbols | Unknown glyph, competing legend, rotated symbol, duplicate tile, adjacent distinct heads, monochrome status |

NIST interaction-testing guidance informs systematic test combinations [R19](10-research-sources.md). Pairwise tests do not prove complete coverage of engineering failure modes.

## Real-project benchmark

Use permissioned, independently reviewed takeoffs. Split datasets by project/originator/revision family, not random tiles. Adjudicate disagreements and label unknowable items. Measure per-type count error, per-size/ledger length error, false connections, missed scope, assembly errors, unresolved rate and total review/correction time.

Document test population, eligibility and exclusions. Avoid a single accuracy percentage. A correct subtotal with missing scope is not a complete correct project.

## Gate policy

For exact synthetic counts require equality. Numerical geometry tolerances are explicit test inputs selected for method/scale, not claims about scan accuracy. Catalogue and real-project targets require domain review before pilot acceptance. Performance, capacity and recovery thresholds are currently unselected; agents must not invent passing production numbers.

Use independent expected values. Do not edit a reference to match an implementation without evidence and a recorded decision. Cross-tool agreement is supporting evidence, not truth if both share the same catalogue error.

## Contract checks in this kit

See `CHECKS.md` for actual local checks. Full third-party OpenAPI conformance, implementation tests, load tests, malicious-file tests, catalogue review and real-project validation are separate future tasks. Do not mistake a documentation package for a tested service.
