# Sprinkler Takeoff Workstation: engineering and agent implementation kit

Version: 0.1.0-design | Research checked: 2026-11-24 | Designed by Noel Chakwenya

This is a researched design and implementation-instruction package, not a completed application, approved sprinkler design, or accuracy certification. It supersedes earlier conceptual documents where its contracts are more specific. Preserve earlier research as background; do not silently import incompatible conventions.

## Start here

1. Read [AGENTS.md](AGENTS.md), then [architecture](docs/01-architecture.md).
2. Read [domain model](docs/02-domain-model.md), [API semantics](docs/03-api-contracts.md) and [symbols/sign conventions](docs/05-symbols-and-signs.md).
3. Use the [formula register](docs/04-formulas.md) and [catalogue/assembly contract](docs/06-catalogue-and-assemblies.md) for implementation.
4. Execute work in the order in [delivery gates](docs/09-delivery-plan.md). Give each agent the appropriate [role brief](agents/README.md).
5. Review [open decisions](docs/11-decisions.md) before selecting third-party components or making accuracy claims.

## What is included

- Architecture, module interfaces and a proposed repository layout.
- Typed engineering data, evidence, revision and quantity semantics.
- Optional HTTP adapter contract in [OpenAPI JSON](contracts/openapi.json), with positive and negative [examples](contracts/examples.json).
- Geometry, cut-length, quantity, uncertainty and later hydraulic formulas.
- Project symbol mapping, engineering signs and accessible UI indicators.
- Catalogue quarantine, physical assembly constraints and source traceability.
- Document/AI proposal workflow, persistence, security and recovery rules.
- Validation matrix, implementation gates and eight agent role briefs.
- A copied [synthetic fixture](fixtures/reference-network.json), explicitly not manufacturer validation.
- A [research source register](docs/10-research-sources.md) linking each evidence-backed choice to primary documentation.
- A [package-check report](CHECKS.md) explaining exactly what was checked and what remains unvalidated.

## Boundaries

The product is Windows-first, local-first, and takeoff-first. Start with reviewed manual reconstruction of straight 3D routes and an explicitly declared assembly subset. Design a model extensible to curves, flexible assemblies and later hydraulics, without claiming those features are already supported.

Recommended desktop shell: C#/.NET/WPF. The CAD renderer, PDF engine, exact runtime version, materials and first product catalogue remain selection decisions. The engineering core must not depend on WPF, a CAD SDK, a cloud service or an AI provider. WPF's Windows scope is documented by Microsoft [R16](docs/10-research-sources.md).

The HTTP design is an optional automation/integration adapter, not a requirement to deploy a web server or cloud application. The primary internal APIs remain in-process contracts.

## How agents should receive this kit

Copy this directory into the application repository or mount it as a read-only design reference. If copying `AGENTS.md` to the repository root, preserve any existing instructions and resolve conflicts using the user's current requirements. Do not overwrite a project's existing agent guidance blindly.

No agent has approval to invent manufacturer data, assert professional sign-off or convert synthetic test success into real-project accuracy. This restriction concerns claims and unsupported engineering data; it does not prevent reversible coding or continued independent work.
