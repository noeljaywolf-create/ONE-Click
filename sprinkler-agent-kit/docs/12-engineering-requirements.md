# Engineering requirements baseline

Version 0.2.0-design, 2026-11-25. Designed by Noel Chakwenya.

This baseline specifies a sprinkler engineering takeoff workstation. Requirements below are proposed product obligations, not assertions of implementation or professional approval. The criteria are our design synthesis; cited sources support individual engineering distinctions. Read with the [formula register](04-formulas.md), [qualification protocol](14-qualification-and-release.md) and [capability matrix](13-capability-and-tolerance-policy.md).

## Intended result and stakeholders

An estimator reconstructs and reviews a physical system from supplied documents. A catalogue reviewer qualifies product data. A domain reviewer decides whether a takeoff is suitable for a stated purpose. A developer implements versioned methods; QA supplies independent cases. One person may fill several roles, but an agent cannot create evidence of an independent review by assigning itself another role.

The differentiator is inspectable physical reconstruction: why each length exists, which assembly creates each purchased item, what is missing, and how revisions affect the result. A realistic-looking 3D model alone is insufficient evidence.

## Requirement language

Each ID below is a testable requirement family. Before implementation, split it into task-sized cases with inputs, outputs, versioned method, negative cases and acceptance evidence. `F` means required in the first qualified release; `L` means later qualified expansion. `F-narrow` means a deliberately restricted, published support envelope. Neither priority means already supported.

| ID | Module / priority | Required behaviour and acceptance evidence |
|---|---|---|
| ENG-001 | Project / F | Record buildings, floors, zones, systems, display units, world frame, issue baseline, included/excluded work and work status. Save/reload must preserve these distinctions. |
| ENG-002 | Documents / F | Preserve original bytes, hash, document number, revision and supersession. A revised sheet creates new identity; previous quantities remain reproducible. |
| ENG-003 | Calibration / F | Support calibrated regions and explicit transforms. Record controls, model, residuals and validation dimensions. Refuse a singular/degenerate transform; flag disagreement. |
| ENG-004 | Geometry / F-narrow | Calculate reviewed straight 3D segments, ordered polylines, slopes, drops and offsets. Unknown elevation makes spatial length incomplete unless a separately justified constraint resolves it. |
| ENG-005 | Geometry robustness / F | Handle zero length, near parallelism, large translations and small local details without topology changes caused by display precision. Robust predicate policy must be tested [R07]. |
| ENG-006 | Geometry extensions / L | Qualify arcs, splines and flexible paths independently. Store analytic geometry or declared approximation/error bounds; never report an unexplained chord as the installed length. |
| ENG-007 | Objects and identity / F | Stable IDs for routes, pieces, components, ports, joints and claims. Split/merge retains ancestry; geometrically coincident distinct objects are not automatically duplicates. |
| ENG-008 | Connectivity / F | Separate candidate proximity from confirmed connection. A crossing, stacked floor or close endpoint does not establish a joint. Test loops, open ends and internal fitting connections. |
| ENG-009 | Pipe specification / F | Nominal designation, material, wall/schedule, actual dimensions, product and joining system remain separate. Size alone cannot select a compatible physical interface. |
| ENG-010 | Catalogue / F-narrow | Pin product revision, exact table/figure, units, dimensional reference and compatibility. Quarantine ambiguous or inconsistent entries; preserve source and review decisions. |
| ENG-011 | End preparation / F-narrow | Represent end-specific groove/thread/socket/flange/weld preparation as applicable. OGS and IGS require distinct interface definitions, not a universal grooved flag [R21,R22]. |
| ENG-012 | Assemblies / F-narrow | Resolve a declared joining family using actual reference planes, compatibility and net offsets. Return resolved, alternatives, infeasible, unsupported or incomplete; do not choose silently. |
| ENG-013 | Material ownership / F | Every included purchased contribution has one owner. Exploded assemblies cannot duplicate bundled contents. Test boundary joints, repeated subassemblies and split routes. |
| ENG-014 | Sprinklers / F-narrow | Preserve product/model identity and purchase variant: orientation, connection, response, K convention, temperature, finish and applicable accessories. Unknown attributes prevent an exact purchase-SKU claim [R23]. |
| ENG-015 | Valves and equipment / F-narrow | Allow explicitly selected valves, trim and equipment assemblies with source-backed included contents. Do not infer a complete riser/valve assembly from one symbol. |
| ENG-016 | Symbols and annotations / F | Project legend controls interpretation. Maintain source crop, class mapping, competing labels and unresolved symbols. Similar graphics across projects may mean different objects. |
| ENG-017 | Properties and claims / F | Important properties carry value state, origin, source, units and derivation. An accepted assumption remains an assumption. Unknown quantity cannot become zero on export. |
| ENG-018 | Review / F | Review purpose and dependency fingerprint are recorded. Changed relevant input makes the review stale. Acceptance of measurement does not imply approval of regulatory suitability. |
| ENG-019 | Takeoff / F | Keep route, fabrication and procurement ledgers separate. Hydraulic ledger is reserved for later analysis. Aggregation preserves unit, specification, work status and source contributions. |
| ENG-020 | Completeness / F | Known/provisional subtotals remain separate. Included unknown contributions force an incomplete total. Omitted unknown objects and silently excluded work are errors. |
| ENG-021 | Purchasing / F-basic, L-advanced | Export itemised qualified demand first. Pack rounding, allowances, stock optimisation and substitutions require explicit policies; shortages cannot be hidden by a project-wide waste factor. |
| ENG-022 | Supports and ancillaries / L | Hangers, anchors, rods, bracing, sleeves and firestopping require arrangements, product constraints and project evidence. A pipe-length ratio is a labelled estimate, not engineered support design. |
| ENG-023 | Model integrity checks / F | Detect invalid geometry, incomplete properties, unresolved connections, incompatible parts, negative cuts, duplicate ownership and stale results. Report scope and evidence per finding. |
| ENG-024 | Standards checks / L-reviewed subset | Each check pins rule source, edition, adoption/applicability, required inputs and test cases. Missing inputs return cannot_verify; an unimplemented applicable check returns unsupported. Never claim whole-system compliance from a subset. |
| ENG-025 | Hydraulics / L | Use actual ID, fluid assumptions, water supply, demands, losses and signed network equations. Verify mass/energy residuals and convergence independently; solver reference [R12] is not certification. |
| ENG-026 | Building context / F-reference, L-analysis | Align floors/sections and reference building geometry. Obstruction, coverage, clashes and penetrations need their own geometric and engineering applicability rules. |
| ENG-027 | Desktop / F | Link drawing, 3D object, properties, issues and contribution. Selecting a quantity highlights its contributing scope. Show unknown/stale/provisional states without relying on colour alone. |
| ENG-028 | Editing / F | Undo/redo is an auditable inverse/new transaction, preserving history. References and dependencies stay valid through edits; rejected partial commands leave no accepted debris. |
| ENG-029 | AI / L | Suggestions contain typed changes, evidence and input fingerprint. Acceptance uses the same commands/invariants as manual work. Source text cannot become an instruction to alter policy or release results. |
| ENG-030 | Snapshots/reporting / F | Freeze sources, catalogue/method/policy versions, quantities, exclusions and review context. PDF/CSV totals agree with the snapshot. Historical exports identify their baseline. |
| ENG-031 | APIs/jobs / F | Typed contracts, project scoping, current preconditions, durable replay, cooperative cancellation and stale-output handling. Complete missing contracts before dependent clients; see API coverage limits. |
| ENG-032 | Persistence/recovery / F | Crash cannot leave a half-accepted command or dangling committed source reference. Backup restores database plus referenced assets and reproduces quantities. |
| ENG-033 | Security/rights / F | Bound untrusted parsing, authenticate optional adapter, control project data sent externally and track licences/content rights. Local reviewer name is attribution, not verified signing authority. |
| ENG-034 | Benchmarks / F | Evaluate on independently reviewed projects held apart from tuning. Report missed and extra items, length errors, SKU errors, unresolved scope and correction effort—not only aggregate total agreement. |
| ENG-035 | Revision comparison / F | Relate old/new objects and sources. Separate added, removed, modified, unchanged and unresolved items; a signed quantity delta does not change demolition into negative procurement. |
| ENG-036 | Performance/operability / F | Measure declared project size/hardware, load/edit/calculate/export latency, memory and restore behaviour. Production thresholds remain open until scope and benchmark evidence are agreed. |

## Interface ownership and additional data

These are engineering interfaces, not extra deployed services. Core owns numerical methods and assembly feasibility; Application owns accepted transactions and policy; Infrastructure owns storage/workers; Desktop owns interaction. Catalogue reviewers supply qualification evidence, never unrestricted table edits to the accepted core.

Add versioned DTOs for `CapabilityProfile`, `TolerancePolicy`, `FormulaDefinition`, `SourceLocator`, `CatalogueQualification`, `AssemblyQualification`, `BenchmarkCase` and `ReleaseAssessment`. Each has stable ID/version, applicable scope, evidence refs and status. The contract agent must define these schemas before consumers depend on them; they are not silently added to the existing OpenAPI.

An engineering value needs more than a confidence percentage. Distinguish document certainty, geometric completeness, catalogue qualification, numerical validity and purpose-specific review. None is a substitute for the others.

## Priorities and unresolved decisions

First qualify manual reconstruction and one real product/joining envelope. Later automation must demonstrate reduced total effort after correction. General curves, supports, full standards checking and hydraulics are designed extensions, not first-release promises.

Before a real fabrication release, choose the first manufacturer/product family and permitted size/material/end-preparation combinations, dimensional conventions, review authority, release purpose and accuracy targets. Reversible core implementation can continue with explicitly synthetic data while those choices are open.
