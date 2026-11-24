# Catalogue release and physical assembly contract

## Catalogue data

Each item carries manufacturer/product/SKU, nominal designation, actual OD/ID where relevant, material/specification, interfaces, port local frames, dimensional references, included contents, allowed application, source document/page/revision/hash and review record. Record whether a dimension is nominal, tolerance range, required setting or allowable maximum.

Ingestion states: `staging -> quarantined | qualified`. A qualified release is immutable. Projects explicitly adopt a release; no live catalogue refresh may change old quantities.

Checks: positive and coherent dimensions, inch/mm conversions within source rounding, OD>ID, wall plausibility, valid unit normals, finite transforms, available combinations, included accessory ownership and source completeness. A conflict blocks the affected property/adapter, not necessarily unrelated known head counts.

Manufacturer fitting/coupling publications demonstrate product-specific takeouts, interface restrictions and limitations on interpreting separation dimensions [R17,R18](10-research-sources.md). Never convert maximum allowable pipe-end separation into a required assembly gap without applicable instructions.

## JoiningAdapter input/output

```text
ResolveJoint(
  frozenPortTopology,
  selectedPipeSpecifications,
  pinnedParts,
  dimensionalConventions,
  fixedAndBoundedGeometry
) -> {
  state: resolved | alternatives | infeasible | unsupported | incomplete,
  candidateAssemblies,
  cutEndPlanes,
  materialOwnership,
  residuals,
  issues,
  inputFingerprint
}
```

Separate adapters for grooved, threaded, welded, socket, flanged and flexible systems. Initially implement only one qualified family. Synthetic net-offset fixtures do not qualify a real adapter.

## Hard constraints

Sizes/interfaces/preparations must be compatible. Product availability and application limits are explicit. Mating position/orientation/clocking must satisfy approved fit conditions. Fixed reviewed dimensions cannot be moved by an optimization objective. Route angles may be arbitrary in the representation while no qualified part arrangement exists.

When multiple arrangements are valid, retain alternatives and provenance. Selecting an arrangement is a command on the expected project revision. Recompute dependent pieces and counts atomically. Negative or process-invalid cuts create issues; never silently suppress them from stocklisting.

## Purchase ownership

Each physical purchase contribution has one owner. A terminal kit can be procured as one SKU or exploded into constituent purchased parts under policy. Existing retained items do not automatically become new purchases; relocated items may add new connection material. Boundary joints have explicit project ownership.

## Qualification evidence

For each real adapter: independent dimensioned assembly cases, exact catalogue versions, source semantics, verified parts list, cut-plane geometry, minimum/preparation constraints and both fit/rejection cases. Manufacturer-source inconsistency triggers quarantine and technical resolution, not automatic silent correction.
