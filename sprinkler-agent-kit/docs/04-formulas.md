# Formula register and numerical contracts

These are implementation specifications with testable limits, not approved design criteria. Source IDs link to [research](10-research-sources.md). SI internally; actual ID for hydraulics; no manufactured constants derived from nominal size.

| ID | Formula / algorithm | Applicability and failure handling | Independent case |
|---|---|---|---|
| G01 | `L = hypot(dx,dy,dz)` | Straight segment with finite known endpoints; unknown elevation -> unknown spatial length | (3,4,12) displacement -> 13 m |
| G02 | `sum(norm(p[i+1]-p[i]))` | Ordered polyline, include intermediate offsets | 3 m then 4 m -> 7 m |
| G03 | `angle = atan2(norm(cross(u,v)),dot(u,v))` | Nonzero unit travel directions; outward mating normals use opposite convention | Orthogonal directions -> pi/2 |
| G04 | `t=clamp(dot(p-a,b-a)/dot(b-a,b-a),0,1)` | Point/segment closest point; zero-length segment -> point distance | p=(2,1,0), segment 0..1 on X -> sqrt(2) |
| G05 | Minimize `norm(a+s*u-b-t*v)^2`, s,t in [0,1] | Use robust constrained closest-points method; do not independently clamp infinite-line solution [R08] | Parallel, near-parallel, endpoint and degenerate cases |
| G06 | `orient2d=(bx-ax)*(cy-ay)-(by-ay)*(cx-ax)` | Use robust sign predicate near degeneracy [R07]; measurement tolerance separate | Collinear and almost-collinear inputs |
| G07 | `arcLength=abs(radius*sweepRadians)` | Known circular arc; preserve intended sweep, not shortest arc | r=2 m, sweep=pi/2 -> pi m |
| G08 | `integral(norm(r'(t)),a,b)` | Later supported curves with error/convergence contract; no silent chord replacement | Compare analytic circle/helix |
| C01 | `s=D/d; L=s*l` | Valid region, d>0; page transform known | 12 m/2400 px; 730 px -> 3.65 m |
| C02 | `s=sum(w*d*D)/sum(w*d*d)` | Weighted fit assumes appropriate measurement-error model; held-out residuals required | Known controls plus bad outlier |
| C03 | `p'=s*R*p+t` or homogeneous H mapping | Minimum adequate transform; homography planar only [R09] | Round-trip rotated region |
| A01 | `Lcut=Dref-netOffsetA-netOffsetB` | Net signed offsets already include approved joint effects; reject nonpositive/below-supported-minimum result | 6000-150-100-3-3=5744 mm under stated artificial convention |
| A02 | `Lcut=mouthClear+insertA+insertB` | Socket-mouth reference convention only; never combine with A01 without converting references | 1000+25+25=1050 mm |
| A03 | `positionResidual=norm(pA-pB)`; angular residual `acos(clamp(-dot(nA,nB),-1,1))` | Unit outward normals; keyed interfaces need clocking; fit limits are product/convention data | Opposed normals -> zero residual |
| Q01 | Sum unique owned contributions by ledger/spec/group | Exclude no included item silently; deduplicate assembly ownership | 21 m route vs 19.77 m synthetic cut |
| Q02 | `allowance=basis*w`; `packs=ceil(required/packSize)` | w and pack policy explicit; unknown basis -> unknown; packSize>0 | Required 13, pack 5 -> 3 packs/15 units |
| Q03 | `metalVolume=pi/4*(OD^2-ID^2)*L`; `mass=rho*volume` | Uniform ideal pipe; OD>ID>0; prefer qualified catalogue mass for real product | Dimensional consistency |
| Q04 | `ceil(sum(cutLengths)/usableStock)` | Lower bound only, ignores fragmentation/kerf | Three 4 m cuts require three 6 m sticks, not two |
| Q05 | Binary bin assignment with capacity, kerf, trim and stock constraints | Later optimization; feasible != proven optimum [R11] | Compare exhaustive tiny cases |
| U01 | `variance(y) ~= J*Cov(x)*transpose(J)` | First-order continuous input model [R10]; include correlated scale; not a substitute for absent routing | 0.5% shared scale on 100 m -> 0.5 m scale contribution |
| U02 | Bounds on `sqrt(h^2+z^2)` over declared z interval | Single straight segment only; unknown intermediate route has no finite upper bound without constraints | Interval crossing z=0 |

## Offset formulas

For an ideal two-bend dogleg with perpendicular offset O and equal deflection theta, `diagonal=O/sin(theta)` and `advance=O/tan(theta)`. Require valid angles and frame. These are intersection-route dimensions; catalogue elbows change physical cut lengths. A rolling offset uses the transverse magnitude in the route's local orthogonal frame, not an arbitrary world projection.

## Later hydraulic module (not first-slice API support)

- H01: `q=K*sqrt(Pg)` in a declared compatible K/flow/pressure convention. Converting gpm/sqrt(psi) K directly into SI without conversion is invalid.
- H02: `v=4*q/(pi*ID^2)`; `h_pressure=Pg/(rho*g)`.
- H03: `hf=fDarcy*(L/ID)*v^2/(2*g)`; `Re=abs(v)*ID/nu`; laminar fully developed circular flow uses `fDarcy=64/Re`. Handle zero/transition/turbulence with declared methods.
- H04: `hf=10.67*L*abs(q)^1.852/(C^1.852*ID^4.871)` for this SI Hazen-Williams convention, q in m3/s, dimensions in m, loss in m water; application constraints required.
- H05: `hminor=Kloss*v^2/(2*g)`; Kloss is not sprinkler K. Avoid double counting the same fitting's equivalent length and loss coefficient.
- H06: solve continuity and energy together using a validated network solver; EPANET provides an algorithm reference, not sprinkler design certification [R12]. Signs are fixed in [05](05-symbols-and-signs.md).

## Formula-change rule

Every method has a stable ID, implementation version, unit contract, assumptions, expected cases and failure behavior. Any change affecting a quantity creates a new result version and review impact. Numerical tolerance must be selected for a method and tested, not borrowed from pixel scale or a manufacturer fit limit. Never use display rounding to force a test to pass.
