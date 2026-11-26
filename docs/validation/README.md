# Validation and qualification evidence scaffold

## Purpose

This directory makes **Limitation 4** explicit and actionable:

> The current repository evidence is **synthetic-tested**. There is **no independent review**, **no real-project validation**, **no manufacturer qualification**, and **no NFPA certification or compliance claim**.

This scaffold does **not** create that evidence. It defines where evidence must be recorded, what claims are allowed at each evidence level, and which acceptance cases remain unvalidated.

## Hard boundaries

- **Unknown is not zero.** Missing evidence remains missing; do not convert it into a pass.
- **Synthetic baseline success is not real-project accuracy.** The copied fixture in `sprinkler-agent-kit/fixtures/reference-network.json` and related tests are documented synthetic checks only.
- **No NFPA certification/compliance claims.** Standards checks, if ever implemented, must follow the applicability/cannot-verify/unsupported rules in `sprinkler-agent-kit/docs/14-qualification-and-release.md`.
- **No manufacturer qualification claims.** Product qualification requires reviewed manufacturer sources, exact dimensional semantics, and negative cases for the named product envelope.
- **No independent review claim by an agent.** An agent may prepare evidence packages; it cannot award Q3/Q4/Q5 reviewer sign-off.

## Contents

| File | Purpose |
|---|---|
| `validation-status-report.md` | Current honest status: synthetic-tested vs unvalidated. |
| `acceptance-case-tracker.json` | Machine-readable AC-001..AC-020 tracker. Statuses are evidence states, not aspirations. |
| `acceptance-case-tracker.md` | Human-readable AC-001..AC-020 tracker generated from the same information model. |
| `evidence-record-template.md` | Template for recording a single evidence artifact/case run. |
| `benchmark-case-template.md` | Template for a future independent real-project benchmark case. |
| `release-assessment-template.md` | Template for a purpose-specific release assessment. Do not use it to claim release without required evidence. |

## Evidence levels

Use the Q0-Q5 ladder from `sprinkler-agent-kit/docs/14-qualification-and-release.md`:

| Level | Claim allowed | Minimum evidence |
|---|---|---|
| Q0 Specified | Method/support scope documented | Inputs, outputs, units, signs, limitations, planned cases |
| Q1 Implemented | Code exists in declared environment | Build + executable case outputs; no real-project accuracy claim |
| Q2 Reference-verified | Agrees with independent reference cases within declared limits | Independent expected cases, adversarial cases, versioned report |
| Q3 Product-qualified | Named product/interface envelope qualified | Reviewed manufacturer sources, dimensional semantics, compatibility, negative cases |
| Q4 Workflow-validated | Declared workflow validated against independent project benchmark | Frozen inputs/truth, held-out projects, predeclared targets, measured results |
| Q5 Purpose-released | Named version may be used within explicit envelope/purpose | Required Q-levels, resolved release assessment, authorized decision, known limitations |

## How to update

1. Add or update an evidence record using `evidence-record-template.md`.
2. Update `acceptance-case-tracker.json` with the new status and evidence reference.
3. Run `python tools/check_validation_scaffold.py` from the repository root.
4. Run `python sprinkler-agent-kit/tools/check_contracts.py` from `sprinkler-agent-kit/` when kit docs/contracts change.
5. Never mark a case `passed` without an evidence reference. Never mark Q3/Q4/Q5 without the required independent review/qualification artifacts.

## Test categorization traits

Use xUnit traits to make evidence scope visible in test runs:

- `[Trait("ValidationEvidence", "SyntheticBaseline")]` - synthetic fixture/baseline only; not real-project/manufacturer/NFPA evidence.
- `[Trait("ValidationEvidence", "SyntheticCase")]` - synthetic acceptance-case coverage; not independent review or real-project validation.
- `[Trait("ValidationScope", "FixtureOnly")]` - depends on the documented synthetic fixture.
- `[Trait("AcceptanceCase", "AC-###")]` - maps a test to a mandatory acceptance case.

Example filter:

```powershell
dotnet test --filter "ValidationEvidence=SyntheticBaseline"
dotnet test --filter "AcceptanceCase=AC-001"
```

## Automated check

`tools/check_validation_scaffold.py` validates the scaffold structure and enforces honest status rules:

- AC-001..AC-020 are present exactly once.
- Statuses are from the allowed set.
- Evidence-required statuses require evidence refs; vague placeholder refs are rejected.
- High-evidence statuses (`reference_verified`, `product_qualified`, `workflow_validated`, `purpose_released`) are blocked until real reviewed evidence is recorded.
- The status report and tracker mention the synthetic-only limitation.
- Obvious positive certification/qualification/validation claim phrases are flagged when not negated.
