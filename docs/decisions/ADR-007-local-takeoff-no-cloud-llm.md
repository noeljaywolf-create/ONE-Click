# ADR-007: Local takeoff only - no cloud LLM dependency

- **Status:** Accepted
- **Date:** 2026-11-27
- **Owners:** Product/Application/Core
- **Related:** `sprinkler-agent-kit/AGENTS.md`, `sprinkler-agent-kit/docs/01-architecture.md`, `sprinkler-agent-kit/docs/07-documents-jobs-and-recovery.md`, `sprinkler-agent-kit/docs/11-decisions.md`

## Context

ONE-Click is a Windows-first, local-first sprinkler takeoff workstation. The core value is inspectable, deterministic takeoff: quantities come from a reviewed physical model, versioned calculation methods, and traceable provenance. The kit already requires that AI proposals cannot silently mutate reviewed engineering state and that local manual modeling/calculation/reporting work offline.

We need an explicit product decision that takeoff does not depend on OpenAI or any cloud LLM.

## Decision

1. **Core takeoff is fully local/offline.** Geometry, topology, assembly resolution, quantities, snapshots, and exports MUST run locally without OpenAI, cloud LLMs, or external AI services.
2. **No OpenAI/cloud LLM dependency in `Core` or `Application`.** No OpenAI SDK, API keys, model IDs, prompt endpoints, or network calls in the takeoff path.
3. **Product “agents” are local deterministic workers/services.** They are bounded local processes or in-process services with typed inputs/outputs, explicit provenance, and command/validation boundaries. They may use deterministic rules, algorithms, local parsers, or local ML models only when explicitly qualified and offline-capable; they are not cloud chat agents.
4. **External AI is optional, proposal-only, and never required.** If ever added, it must be isolated behind an explicit adapter, require project data policy/consent, produce typed proposals with evidence/input fingerprints, and never mutate reviewed engineering state or release takeoff results.
5. **UI/CLI capability parity.** Every takeoff action required for the supported workflow must be possible with no AI service configured.

## Consequences

### Positive

- Deterministic, auditable takeoff behavior.
- Offline operation and simpler data-rights posture by default.
- Clear security boundary: untrusted document content is not sent to cloud LLMs.
- Easier testing/reproducibility: same inputs + versions => same outputs within declared policy.

### Costs / Tradeoffs

- More engineering effort for deterministic parsing, geometry, and review workflows.
- AI-assisted UX, if desired later, requires careful proposal/review design and explicit consent controls.
- Local model/runtime packaging may be needed for future document assistance, but must remain optional and qualified.

## Guardrails

- Code review rejects OpenAI/cloud-LLM dependencies in Core/Application takeoff paths.
- Worker outputs are typed proposals or typed results; no direct domain-table writes.
- Any future AI adapter must record provider/model/version, data scope, consent, and input fingerprint.
- Tests must cover local/offline execution of first-slice takeoff flows.

## Revisit triggers

- A user-approved, documented requirement for optional cloud-assisted proposal generation with explicit data-rights controls.
- A qualified local ML/document model that improves correction time while preserving offline operation and review boundaries.