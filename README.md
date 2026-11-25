# ONE-Click

Windows-first, local-first sprinkler takeoff workstation.

## Stack (per design kit)

- Desktop shell: C#/.NET/WPF
- Engineering core: deterministic, in-process, independent of WPF/CAD SDK/cloud/AI
- Optional HTTP adapter: integration only, not required deployment

## Repository layout

- `sprinkler-agent-kit/` - design/engineering requirements and agent implementation kit (read-only reference)
- `src/Core` - units, geometry, topology, assembly, quantities (pure/deterministic)
- `src/Application` - commands, queries, transaction boundary
- `src/Infrastructure` - persistence, workers integration, external adapters
- `src/Desktop` - WPF UI
- `src/Workers` - bounded document/AI workers
- `src/HttpAdapter` - optional HTTP adapter
- `contracts/` - API/schema contracts
- `catalogues/` - catalogue staging/qualified data
- `tests/Reference`, `tests/Integration`, `tests/Recovery`
- `docs/decisions/` - ADRs and project decisions

## Status

Scaffold only. No engineering behavior is implemented or qualified yet. Follow `sprinkler-agent-kit/AGENTS.md` and delivery gates in `sprinkler-agent-kit/docs/09-delivery-plan.md`.
