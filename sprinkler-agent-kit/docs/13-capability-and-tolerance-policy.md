# Capability envelope and tolerance policy

Proposed specification, 2026-11-25. No production capabilities are qualified by this document.

## Capability is a versioned engineering input

An application feature flag is insufficient. A `CapabilityProfile` identifies supported geometry, joining system, material/size range, product revisions, permitted transformations, quantity ledgers, methods, limitations and qualifying cases. Installation of an engine does not qualify every geometry it can render.

| Capability | First qualification target | Explicit boundary / required evidence |
|---|---|---|
| Straight spatial route | Known finite 3D endpoints, slopes and verticals | Independent length cases; missing Z retained as unknown |
| Polyline / dogleg | Ordered straight segments | No automatic elbow selection; local angle/reference convention recorded |
| Connection graph | Explicit reviewed ports and joints | Loops allowed; no crossing/proximity inference as fact |
| Physical cut | One qualified family with pinned net offsets | Product-specific reference planes and assembly cases; synthetic offsets cannot qualify a product |
| Sprinkler takeoff | Reviewed class count; exact SKU only with sufficient product data | Unknown finish/temperature/connection may permit head count but not exact purchase variant |
| Grooved interfaces | Separately qualified named groove family | OGS/IGS interchangeability must not be inferred [R21,R22] |
| Threaded/socket/flanged/welded | Later or individually selected first family | Engagement, insertion, faces and fabrication conventions need separate adapters |
| Curved/flexible | Later | Actual routed/developed length, end connections, restrictions and approximation contract |
| Supports/bracing | Later | Declared layout/loading/design basis, qualified products and distinct review |
| Hydraulics | Later | Physical-to-analysis projection, boundary conditions, actual IDs and independently verified solution |
| Automated recognition | Later | Project-separated evaluation and manual correction cost |

## Runtime capability decisions

Evaluate each object, joint and requested calculation against its exact profile. Do not silently substitute a product, curve approximation or joining adapter. Return the supported portion and explicit affected issues. A complete drawing count can coexist with incomplete fabrication length or unresolved purchasing.

Capability states: `planned`, `implemented_unqualified`, `qualified`, `suspended`. They are separate from value states and review states. `Qualified` requires the evidence and reviewer in the release protocol; a developer cannot set it solely because tests compile.

## Keep four tolerance classes separate

1. **Numerical tolerance:** algorithm-specific comparison/error control, scale-aware and tested. It does not describe installation tolerance.
2. **Measurement uncertainty:** uncertainty of scale, picked points and source dimensions. Include relevant covariance; shared scale error does not average away [R10]. Unknown hidden routing is not a small random measurement error.
3. **Product/assembly allowance:** manufacturer or qualified fabrication limits with exact dimensional meaning and applicability. A listed maximum permitted separation is not necessarily the chosen installation gap.
4. **Decision/release threshold:** project/purpose-specific permitted discrepancy or completeness condition. It requires agreement and benchmark evidence.

Store each separately with unit, coordinate/reference convention, source, method and version. Never pass one ambiguous `epsilon` through the whole system.

For a scalar test where appropriate, a method may use `abs(a-b) <= absTol + relTol*max(abs(a),abs(b))`. This is a proposed comparison policy, not a universal geometry predicate or approved physical fit criterion. Bounds must not hide a wrong-sign result or a genuinely infeasible assembly. Robust orientation signs have a different numerical contract [R07].

## Calibration acceptance

Record source pixel/PDF frame, page rotation/crop, world frame, control coordinates and units, transform method and residuals. Minimum control count alone is insufficient: controls must be distinct and sufficiently distributed/nondegenerate for the selected model. Document the estimator, outlier treatment and independent validation dimensions. A fitted transform may agree with its own controls while being wrong elsewhere.

Do not invent an acceptable millimetre or percentage error for all drawings. Agree a purpose-specific limit and reject/qualify drawings that cannot meet it. Multiple regions may require separate calibrations. A homography does not restore absent 3D elevations or arbitrary nonlinear scan distortions.

## Source qualification examples

Victaulic 25.01 Rev T (03/2026) and 25.14 Rev G (10/2024) describe distinct preparation dimensions [R21,R22]. Treat their field meanings and product applicability as inputs. Importing a table does not verify that a particular coupling, pipe wall and preparation form a valid assembly. No table dimensions are supplied here as preapproved production data.

Viking exposes selection attributes beyond a sprinkler count, including response, orientation, K-factor, temperature and finish [R23]. Accordingly, preserve a partially identified head rather than guessing a purchasable part. These product references establish data requirements, not an endorsement or exclusive manufacturer choice.

## Qualitative limits that can be enforced immediately

- No NaN/infinity accepted as engineering data.
- No omitted unknown contribution in a purported complete included scope.
- No duplicate purchased ownership in a resolved assembly expansion.
- No stale dependency represented as current.
- No incompatible ledger or unit summation.
- No exact product SKU claim from an unresolved product variant.

These are categorical invariants. Numerical accuracy, capacity and response-time targets must be recorded before the corresponding pilot, not invented after observing its result.
