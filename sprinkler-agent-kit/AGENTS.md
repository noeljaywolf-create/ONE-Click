# Agent instructions: sprinkler takeoff workstation

## Mission

Build an inspectable takeoff system whose quantities come from a reviewed physical model and versioned calculation methods. Read `README.md` and the role brief assigned to you before implementing. This file applies to this kit and, when deliberately installed there, the application repository. User instructions take precedence.

## Non-negotiable engineering invariants

1. Unknown is not zero. Preserve a tagged unknown, the issue and affected scope.
2. A plan crossing is not a connection. A nearby endpoint is only a candidate.
3. Nominal size is an identifier, not actual OD/ID. Require catalogue mapping.
4. Keep route, fabrication, hydraulic and procurement ledgers distinct.
5. Count unique physical ownership. Never purchase both an exploded assembly and its included parts without an explicit policy.
6. Catalogue versions and dimensional reference conventions are pinned inputs.
7. AI produces proposals. It cannot silently mutate reviewed engineering state or release a takeoff.
8. Accepting an assumption does not change its origin into a documented fact.
9. Every derived result has input versions, method version and traceable contributions.
10. Changed dependencies invalidate affected results and reviews; snapshots remain immutable.
11. Geometric feasibility, takeoff completeness and regulatory checks have separate outcomes.
12. Runtime code and comments must not claim NFPA certification or universal accuracy.

## Working method

- Implement the smallest complete gate in `docs/09-delivery-plan.md` with meaningful independent expected results.
- Before changing a contract, update the contract, examples, consumers and tests together; document migration/compatibility effects.
- Preserve stable IDs, units, original evidence and split/merge ancestry.
- Use explicit application commands and repository transactions. UI/AI/worker modules do not write domain tables directly.
- Do not import network/UI/renderer dependencies into the engineering core.
- Use primary sources in `docs/10-research-sources.md`. Record the product, revision and exact dimensional meaning for catalogue claims. Mark unsupported details as unresolved.
- Do not implement standards thresholds from memory. Record scope, applicability, edition and professional-review requirement separately.
- Continue reversible implementation on clearly labeled assumptions. Escalate a missing decision only when it prevents correct dependent work; keep unaffected work moving.
- Do not run or send messages through another agent automatically merely because this kit contains roles. The orchestrator assigns work under the user's authorization.

## Required handoff

Report: files changed; implemented behavior; input/output contracts; reference cases run; failures/limitations; source/method versions; next dependency. Distinguish implemented, tested on synthetic data, independently reviewed, and validated on real projects.

## Definition of done for a gate

Required checks pass; negative cases fail safely; results are deterministic for frozen inputs within declared numerical tolerance; unknowns and provenance survive save/reload/export; no new stale-result or duplicate-count path is introduced. Full professional qualification is a separate gate, not a label an agent can award itself.
