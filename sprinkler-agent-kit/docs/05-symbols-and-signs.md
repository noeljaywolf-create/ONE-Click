# Drawing symbols, engineering signs and UI indicators

## Drawing-symbol ontology

Do not establish a universal mapping such as "circle = pendent sprinkler". Source legends and schedules vary. Store `SymbolDefinition(id, projectId, documentRevisionId, legendRegionId, visualTemplateHash, semanticClass, candidateProperties, applicabilityRegions, reviewState)`.

Initial semantic classes: sprinkler candidate, pipe candidate, junction mark, size/elevation annotation, continuation, riser/drop indication, valve/equipment candidate, detail callout and background/non-model annotation. Sprinkler orientation/response/K-factor/product identity are separate properties; a detected icon alone does not establish them all.

NFPA's indexed discussion of NFPA 170 includes fire-extinguishing-system symbols [R13](10-research-sources.md). That is a reference to investigate for project applicability and licensed assets, not authority to override the actual legend or copy a symbol library without checking rights.

## Interpretation rules

1. Preserve observation location, source crop/vector IDs and document revision.
2. Match the governing legend/schedule and record alternatives.
3. Allow rotated/scaled variants with documented normalization.
4. Separate new/existing/demolition line styles from physical specifications.
5. Reconcile tile overlaps and repeated details without deleting distinct nearby items.
6. Require explicit connection evidence for crossings/junctions.
7. Unknown symbol creates an issue and source marker; it does not disappear.

Do not overwrite original drawing graphics. UI overlays are application annotations.

## Coordinate and hydraulic signs (our convention)

| Concept | Fixed convention |
|---|---|
| World frame | Right-handed, +Z up; +X/+Y defined by project grids |
| Drawing/raster | Source-specific axes; transform explicitly; raster y-down never assumed to be world y-down |
| Rotation | Right-hand rule, radians; record local/world order |
| Route parameter | Increasing from recorded start to end; reversing preserves length |
| Port normal | Outward from component; ideal mating normals oppose |
| Flow q_ij | Positive from node i to node j |
| Incidence B | +1 at outgoing/tail, -1 at incoming/head |
| Demand d_i | Positive withdrawal; continuity at unknown junctions `B*q+d=0` |
| Head H_i | `z_i+Pg_i/(rho*g)` in stated water-network model; Pg is gauge pressure |
| Signed resistance | `h_ij=r*q_ij*abs(q_ij)^(n-1)`; pumps/devices require their own signed model |
| Energy residual | `H_i-H_j-h_ij=0` for passive link convention |
| Upward pressure need | Additional static pressure magnitude `rho*g*(z_j-z_i)` for positive rise |
| Material removal | Positive quantity with workStatus=demolished, not a negative purchased count |
| Revision delta | New minus old compatible-ledger quantity; can be negative |

This convention differs from some texts' incidence signs; adapters must translate explicitly. EPANET equations inform the physics [R12](10-research-sources.md); our API signs are a deliberate internal convention.

## Visual status contract

Use labels/icons/patterns in addition to color. Proposed overlays: `K` known, `~` provisional, `?` unknown, `!` conflict/failure, `R` reviewed, `S` stale. These are application labels, not claimed NFPA drawing symbols. Include a visible legend and accessible text. `R` never hides `~` on an accepted assumption.

Show completeness, engineering-check result, review purpose and freshness separately. Green alone must not imply complete compliance. Exports retain labels and assumptions in monochrome. Symbol-set/status changes are versioned and tested against existing projects.
