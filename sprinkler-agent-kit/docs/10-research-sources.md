# Research evidence register

Checked 2026-11-24. These are primary-source method references, not endorsements or proof the proposed application is accurate. The package's module decomposition, API policy, state unions, UI indicators, release gates and fixture conventions are original design synthesis. Where the site returned no readable body, that limitation is stated. No source grants blanket permission to redistribute standards, manufacturer catalogues or symbol artwork.

Engineering-baseline addendum checked 2026-11-25: R21-R23 added; R07, R10 and R12 revisited. Other entries retain their original check date. These references inform requirements; no production catalogue has been qualified.

| ID | Source | Supports / limit |
|---|---|---|
| R01 | [OpenAPI 3.1.1](https://spec.openapis.org/oas/v3.1.1.html) | Pinned HTTP description/schema semantics; not latest-version claim |
| R02 | [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html) | HTTP conditional requests, strong validators and 412; replay retention is our policy |
| R03 | [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html) | Problem-details envelope; our extension codes remain application-defined |
| R04 | [JSON Schema 2020-12 validation](https://json-schema.org/draft/2020-12/json-schema-validation) | Tagged-union structural validation; cannot prove geometric/business invariants |
| R05 | [SQLite Online Backup API](https://www.sqlite.org/backup.html) | Consistent database snapshot approach; external asset consistency needs our protocol |
| R06 | [W3C PROV-DM](https://www.w3.org/TR/prov-dm/) | Entity/activity/agent/derivation concepts; RDF implementation not required |
| R07 | [Shewchuk robust predicates](https://www.cs.cmu.edu/~quake/robust.html) | Adaptive exact sign decisions, not exact measured geometry |
| R08 | [Eberly segment distance](https://www.geometrictools.com/Documentation/DistanceLine3Line3.pdf) | Robust constrained closest-points method; implementation license review needed |
| R09 | [OpenCV homography](https://docs.opencv.org/4.13.0/d1/de0/tutorial_py_feature_homography.html) | Planar mapping/inlier estimation; no solution for missing elevations or arbitrary scan warp |
| R10 | [NIST TN 1297 Appendix A](https://www.nist.gov/pml/nist-technical-note-1297/nist-tn-1297-appendix-law-propagation-uncertainty) | First-order uncertainty and covariance with justified inputs |
| R11 | [OR-Tools bin packing](https://developers.google.com/optimization/pack/bin_packing) | Assignment/capacity optimization; cutting process needs extra constraints |
| R12 | [EPA EPANET algorithms](https://usepa.github.io/EPANET2.2/12_analysis_algorithms.html) | Continuity/energy/network solution; sprinkler design applicability separate |
| R13 | [NFPA discussion of symbols](https://www.nfpa.org/news-blogs-and-articles/blogs/2025/03/14/signs-and-symbols-in-nfpa-704-and-nfpa-170) | Official indexed scope summary; direct page had no readable body; no glyphs copied |
| R14 | [Tesseract quality guidance](https://tesseract-ocr.github.io/tessdoc/ImproveQuality.html) | OCR preprocessing and segmentation sensitivity |
| R15 | [PDFium fpdfview API](https://pdfium.googlesource.com/pdfium/+/refs/heads/main/public/fpdfview.h) | Page/render API and thread-safety note; not semantic sprinkler extraction |
| R16 | [Microsoft WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) | Windows desktop framework scope; no selected CAD engine |
| R17 | [Victaulic 10.03](https://assets.victaulic.com/assets/uploads/literature/10.03.pdf) | Retrieved revision R, 08/2024, product-specific dimensions/interfaces |
| R18 | [Victaulic 10.64](https://assets.victaulic.com/assets/uploads/literature/10.64.pdf) | Retrieved revision W, 01/2026, coupling preparation/application/separation qualifications |
| R19 | [NIST SP 800-142](https://csrc.nist.gov/pubs/sp/800/142/final) | Interaction-test planning; not exhaustive engineering validation |
| R20 | [NFPA 13 development record](https://www.nfpa.org/codes-and-standards/nfpa-standard-development-process/13) | Official indexed edition/update tracking; direct page no readable body on this pass; local adoption and actual rule text still required |
| R21 | [Victaulic OGS specifications 25.01](https://assets.victaulic.com/assets/uploads/literature/25.01.pdf) | Retrieved Rev T, 03/2026, pp. 1-3 and preparation notes: separate groove/preparation fields and applicability; not a generic coupling gap or assembly qualification |
| R22 | [Victaulic IGS specifications 25.14](https://assets.victaulic.com/assets/uploads/literature/25.14.pdf) | Retrieved Rev G, 10/2024, p. 1 dimensions/notes: distinct named interface and end preparation; no claim of OGS interchangeability |
| R23 | [Viking sprinkler selector](https://www.vikinggroupinc.com/digital/webtools/sprinkler-selector) | Manufacturer identifies product selection attributes, part-number details, accessories and technical data; selection UI is not a full product qualification record |

## Evidence hierarchy for future changes

Mathematical definition / original algorithm -> applicable manufacturer revision -> governing project/standard text -> independent test evidence. Distinguish required criteria from examples, proposed amendments and commentary. Reverify dynamic source revisions when qualifying a release. If a reference is inaccessible, record the limit and do not invent its contents.
