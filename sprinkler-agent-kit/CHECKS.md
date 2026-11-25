# Package verification

Checked: 2026-11-25. Run `python tools/check_contracts.py` from this directory.

The bundled checker verifies local references, operation IDs and path parameters, required replay/precondition headers, positive and negative examples using a limited schema evaluator, local Markdown links, formula arithmetic, ledger boundaries, and the copied fixture's 21 m route / 19.77 m cut baseline. See the console report for actual counts. These checks apply to the design package only.

Recorded result: PASS for 150 resolved references, 6 accepted examples, 5 rejected examples, 10 operation checks, 53 existing Markdown link targets, 6 formula arithmetic cases, the synthetic route/cut baseline and the ledger boundary check. The checker also verifies unique sequential IDs for the 36 requirement families and 20 planned acceptance cases, plus source citation registration. These additional checks inspect documentation structure; they do not execute the planned acceptance cases. Full output is retained in `contracts/check-results.json`.

The checker is deliberately **not** a complete OpenAPI or JSON Schema validator. It does not implement every standard keyword or prove the contract's business semantics. A complete pinned validator must be added to application CI before generating clients or shipping an adapter. No third-party full validator was available for this package check.

No production geometry engine, catalogue adapter, HTTP service, desktop UI, concurrency, authentication, crash recovery, or real-project accuracy was tested. The fixture's declared component counts are not independently inferred. Source links were researched separately; the local checker does not test online availability or certify the referenced content.

## Contract coverage limit

The OpenAPI document covers the initial command/job orchestration seam. It is not a complete desktop backend. Detailed region creation/query, evidence and proposal retrieval, model queries, deletion/split/merge, catalogue administration, artifact download/streaming, issue queries and history require explicit contracts in the relevant delivery gate. Internal module interfaces identify these responsibilities; agents must extend and review the schema before implementing callers that depend on them. Do not invent incompatible private HTTP endpoints.

Release criteria, manufacturer data, supported joining families and engineering tolerances still require qualification. Draft specifications and passing arithmetic checks do not establish professional acceptance.
