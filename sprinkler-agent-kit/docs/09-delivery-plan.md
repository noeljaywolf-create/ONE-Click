# Dependency-based delivery plan

Dates/effort estimates require team and catalogue scope. The gate order is a design recommendation. Work within a gate may proceed independently after contracts are stable; integration is explicit.

| Gate | Deliverable | Required evidence | Agent roles |
|---|---|---|---|
| 0 | Contract baseline, units, scope, decision log | Reviewed schemas/examples, ledger/sign consistency, acknowledged open decisions | Coordinator, contracts, QA |
| 1 | Pure geometry and typed values | Independent analytical, degenerate, invariant tests | Core, QA |
| 2 | Ports, network, catalogue staging and one adapter | Crossings/compatibility, quarantine, dimensioned assembly cases | Core, catalogue, QA |
| 3 | Accepted-state store, commands and revisions | Atomic changes, replay/conflict tests, save/reload | Persistence, contracts, QA |
| 4 | Manual document calibration and linked workspace | Known drawing reconstruction; source traceability; unknowns visible | Documents, desktop, core |
| 5 | Qualified takeoff, snapshots and exports | Reference network, independent quantities, no silent omissions | Core, persistence, desktop, QA |
| 6 | Recovery and real-project pilot | Crash/restore, independent project benchmark, predeclared targets | QA, coordinator, domain reviewer |
| 7 | Selective automation | Project-separated measurements and reduced total correction effort | Documents, QA |
| Later | Procurement optimizer, curves/flexible, supports, hydraulics, BIM | Separate support matrix and qualified cases per expansion | Assigned after evidence |

## First build slice

Load the synthetic network, display geometry, change one elevation, show route/cut ledgers, make one elevation unknown, and export a frozen qualified result. Do not start with AI detection or an elaborate dashboard. This proves the shared contracts and uncertainty behavior.

## Required domain decisions

Select first real pipe/connection family and catalogue source; estimating versus fabrication release; initial market/project criteria; supported document quality; project capacity/hardware; recovery and latency objectives; review identity and signing; data rights and external AI policy.

Proceed with synthetic contracts while decisions remain open. Do not label synthetic dimensions as a real manufacturer catalogue or promise a release capability requiring unresolved decisions.

## Agent handoff template

```text
Task ID / assigned role:
Read contracts and decision IDs:
Scope implemented:
Changed files and contract versions:
Checks run with actual results:
Source/catalogue/method versions:
Unknowns and remaining evidence:
Dependencies ready for next role:
```
