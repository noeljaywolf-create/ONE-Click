"""Validation scaffold checks.

Purpose: verify the validation/qualification evidence scaffold is present and
honest. This script does NOT validate engineering correctness, real-project
accuracy, manufacturer qualification, or NFPA compliance/certification.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VALIDATION_DIR = ROOT / "docs" / "validation"
TRACKER_JSON = VALIDATION_DIR / "acceptance-case-tracker.json"
TRACKER_MD = VALIDATION_DIR / "acceptance-case-tracker.md"
STATUS_REPORT = VALIDATION_DIR / "validation-status-report.md"
README = VALIDATION_DIR / "README.md"
TEMPLATES = [
    VALIDATION_DIR / "evidence-record-template.md",
    VALIDATION_DIR / "benchmark-case-template.md",
    VALIDATION_DIR / "release-assessment-template.md",
]

ALLOWED_STATUSES = {
    "not_started",
    "specified",
    "synthetic_tested",
    "reference_verified",
    "product_qualified",
    "workflow_validated",
    "purpose_released",
    "blocked",
}
EVIDENCE_REQUIRED_STATUSES = {
    "synthetic_tested",
    "reference_verified",
    "product_qualified",
    "workflow_validated",
    "purpose_released",
}
# Phrases that would be dishonest positive claims in this scaffold.
FORBIDDEN_POSITIVE_PATTERNS = [
    r"\bNFPA\s+certified\b(?!\s*/compliant\s*claim)",  # checked with negation filter below
    r"\bNFPA\s+compliant\b",
    r"\bmanufacturer[- ]qualified\b",
    r"\bindependently\s+reviewed\s+and\s+approved\b",
    r"\breal[- ]project\s+validated\b",
    r"\buniversally\s+accurate\b",
]
# Lines containing these negation markers are allowed to mention forbidden phrases.
NEGATION_MARKERS = [
    "not ",
    "no ",
    "never ",
    "without ",
    "does not",
    "do not",
    "cannot",
    "can't",
    "unvalidated",
    "missing",
    "limitation",
    "does **not**",
    "no nfpa certification/compliance claim",
]


class CheckFailure(Exception):
    pass


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as e:  # pragma: no cover - diagnostic path
        raise CheckFailure(f"Failed to parse JSON {path}: {e}") from e


def assert_true(condition: bool, message: str, failures: list[str]) -> None:
    if not condition:
        failures.append(message)


def check_required_files(failures: list[str]) -> None:
    required = [README, TRACKER_JSON, TRACKER_MD, STATUS_REPORT, *TEMPLATES]
    for path in required:
        assert_true(path.exists(), f"Missing required file: {path.relative_to(ROOT)}", failures)


def check_tracker(failures: list[str]) -> dict:
    data = load_json(TRACKER_JSON)
    cases = data.get("cases")
    assert_true(isinstance(cases, list), "tracker.cases must be a list", failures)
    if not isinstance(cases, list):
        return {"cases": []}

    ids = [c.get("id") for c in cases]
    expected = [f"AC-{i:03d}" for i in range(1, 21)]
    assert_true(ids == expected, f"AC IDs must be AC-001..AC-020 in order; got {ids}", failures)
    assert_true(len(ids) == len(set(ids)), "AC IDs must be unique", failures)

    status_counts: dict[str, int] = {s: 0 for s in sorted(ALLOWED_STATUSES)}
    for case in cases:
        cid = case.get("id", "<missing>")
        status = case.get("status")
        refs = case.get("evidence_refs")
        limitations = case.get("limitations")
        evidence_level = case.get("evidence_level")
        assert_true(status in ALLOWED_STATUSES, f"{cid}: invalid status {status!r}", failures)
        if status in status_counts:
            status_counts[status] += 1
        assert_true(isinstance(refs, list), f"{cid}: evidence_refs must be a list", failures)
        assert_true(isinstance(limitations, str) and limitations.strip(), f"{cid}: limitations required", failures)
        assert_true(isinstance(evidence_level, str) and evidence_level.strip(), f"{cid}: evidence_level required", failures)
        if status in EVIDENCE_REQUIRED_STATUSES:
            assert_true(bool(refs), f"{cid}: status {status} requires evidence_refs", failures)
            if isinstance(refs, list):
                for ref in refs:
                    assert_true(isinstance(ref, str) and ref.strip(), f"{cid}: evidence ref must be non-empty string", failures)
                    assert_true("confirm specific case" not in ref.lower(), f"{cid}: vague placeholder evidence ref is not allowed: {ref}", failures)
        if status in {"reference_verified", "product_qualified", "workflow_validated", "purpose_released"}:
            assert_true(False, f"{cid}: status {status} is not allowed in this scaffold until real evidence is recorded and reviewed", failures)

    return {"status_counts": status_counts, "case_count": len(cases)}


def line_has_negation(line: str) -> bool:
    lower = line.lower()
    return any(marker in lower for marker in NEGATION_MARKERS)


def check_forbidden_positive_claims(failures: list[str]) -> None:
    files = [README, TRACKER_JSON, TRACKER_MD, STATUS_REPORT, *TEMPLATES]
    for path in files:
        text = path.read_text(encoding="utf-8")
        for i, line in enumerate(text.splitlines(), start=1):
            lower = line.lower()
            for pattern in FORBIDDEN_POSITIVE_PATTERNS:
                if re.search(pattern, lower, flags=re.IGNORECASE) and not line_has_negation(line):
                    failures.append(
                        f"{path.relative_to(ROOT)}:{i}: potential positive claim matches /{pattern}/: {line.strip()}"
                    )


def check_report_mentions_limitations(failures: list[str]) -> None:
    report = STATUS_REPORT.read_text(encoding="utf-8").lower()
    for needle in ["synthetic-tested", "not independently reviewed", "not real-project validated", "no manufacturer qualification", "nfpa"]:
        assert_true(needle in report, f"Status report must mention '{needle}'", failures)
    tracker_md = TRACKER_MD.read_text(encoding="utf-8").lower()
    assert_true("not independently reviewed" in tracker_md, "Tracker markdown must state not independently reviewed", failures)
    assert_true("ac-001" in tracker_md and "ac-020" in tracker_md, "Tracker markdown must include AC-001..AC-020", failures)


def main() -> int:
    failures: list[str] = []
    check_required_files(failures)
    summary: dict = {}
    if TRACKER_JSON.exists():
        summary = check_tracker(failures)
    check_forbidden_positive_claims(failures)
    if STATUS_REPORT.exists() and TRACKER_MD.exists():
        check_report_mentions_limitations(failures)

    result = {
        "status": "PASS" if not failures else "FAIL",
        "summary": summary,
        "limitations": [
            "This check validates scaffold structure and honesty rules only.",
            "It does not validate engineering correctness, real-project accuracy, manufacturer qualification, or NFPA compliance/certification.",
        ],
        "failures": failures,
    }
    print(json.dumps(result, indent=2))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
