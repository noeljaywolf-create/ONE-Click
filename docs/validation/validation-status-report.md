# Validation Status Report

**Date:** 2026-11-26  
**Status:** Synthetic-tested scaffold only - **not independently reviewed, not real-project validated, not manufacturer qualified, not NFPA certified/compliant**.

## Executive summary

This repository contains synthetic/reference tests and package contract checks. These checks are useful for development regression detection, but they do **not** establish real-project accuracy, manufacturer qualification, NFPA compliance/certification, or release readiness. There is no manufacturer qualification evidence and no independent review sign-off.

The AC-001..AC-020 tracker currently records **2 synthetic_tested** cases and **18 not_started** cases. No case is reference-verified by independent review, product-qualified, workflow-validated, or purpose-released.

## Evidence inventory (current)

| Evidence | Type | Status | What it supports | What it does not support |
|---|---|---|---|---|
| `sprinkler-agent-kit/tools/check_contracts.py` | Package/doc/fixture checker | Passes for limited checks recorded in `sprinkler-agent-kit/CHECKS.md` | Local doc/contract/reference integrity; synthetic fixture arithmetic/baseline checks as documented | Complete OpenAPI/JSON Schema conformance; application behavior; real-project accuracy; manufacturer qualification; NFPA certification |
| `tests/Reference/*` | Automated synthetic/reference tests | Executed in CI/dev as tests; see test run log | Synthetic formula/geometry/quantity behavior on declared cases | Independent review; real-project validation; manufacturer qualification; NFPA certification |
| `tests/Integration/*` | Automated integration tests | Executed in CI/dev as tests; see test run log | Application contract/command boundary behavior on synthetic/in-memory scenarios | Real-project workflow validation; field/manufacturer/NFPA qualification |
| `tests/Recovery/*` | Automated recovery tests | Executed in CI/dev as tests; see test run log | Synthetic recovery scenarios as implemented | Production recovery qualification across all failure modes/environments |
| `docs/validation/*` | Evidence scaffold | Present | Tracking/templates/checklist for future evidence | Any actual qualification or release evidence by itself |

## Synthetic-tested vs unvalidated

### Synthetic-tested (limited)

- Selected geometry/formula unit cases (e.g., AC-001, AC-002) through xUnit reference tests.
- Synthetic fixture baseline calculations (21 m route / 19.77 m cut) as documented in kit checks/tests.
- Package-level contract/doc/fixture consistency checks in `sprinkler-agent-kit/tools/check_contracts.py`.

### Unvalidated / missing evidence

- **Independent review:** No independent domain reviewer sign-off records.
- **Real-project validation:** No permissioned, independently reviewed, held-out project benchmark.
- **Manufacturer qualification:** No reviewed manufacturer/product dimensional envelope, compatibility, or negative-case qualification.
- **NFPA/certification/compliance:** No NFPA certification/compliance claim is made; no applicable standards-check qualification evidence exists.
- **Release assessment:** No purpose-specific release assessment with authorized decision.
- **Most AC cases:** AC-003 through AC-020 have no executable application evidence recorded in this tracker.

## AC-001..AC-020 summary

| Status | Count | IDs |
|---|---:|---|
| synthetic_tested | 2 | AC-001, AC-002 |
| not_started | 18 | AC-003, AC-004, AC-005, AC-006, AC-007, AC-008, AC-009, AC-010, AC-011, AC-012, AC-013, AC-014, AC-015, AC-016, AC-017, AC-018, AC-019, AC-020 |
| reference_verified | 0 | - |
| product_qualified | 0 | - |
| workflow_validated | 0 | - |
| purpose_released | 0 | - |

## Next dependencies

1. **Implement executable AC cases** with independent expected results and negative cases; update `acceptance-case-tracker.json` with evidence refs.
2. **Create evidence records** for every case run using `evidence-record-template.md`.
3. **Obtain independent review** for reference-verified claims; record reviewer identity and scope.
4. **Define manufacturer/product envelope** before any product qualification work; no manufacturer data may be invented.
5. **Run held-out real-project benchmark** only after input freeze, predeclared targets, and adjudication process are defined.
6. **Complete purpose-specific release assessment** before any release claim; keep excluded/unresolved scope explicit.

## Reporting rule

Any external status report must distinguish: **implemented**, **synthetic-tested**, **independently reviewed**, **real-project validated**, **manufacturer qualified**, and **purpose-released**. Absence of evidence is reported as unvalidated, not as pass.
