# Agent assignment briefs

Read root `AGENTS.md` first. These files define responsibilities; they do not automatically launch agents or authorize external messages.

| Role | Brief | Main dependency |
|---|---|---|
| Coordinator | [coordinator.md](coordinator.md) | Decisions, scope and integration |
| Contracts/API | [contracts.md](contracts.md) | Domain/sign semantics |
| Core engineering | [core-engineering.md](core-engineering.md) | Formulas and catalogue adapters |
| Catalogue | [catalogue.md](catalogue.md) | Product data and dimensional review |
| Documents/AI | [documents-ai.md](documents-ai.md) | Sources, transforms and proposals |
| Persistence | [persistence.md](persistence.md) | Commands, revisions and immutable assets |
| Desktop | [desktop.md](desktop.md) | Read models, selection, qualified values |
| Independent QA | [qa.md](qa.md) | References established separately from implementation |

Give an agent one bounded gate/task with owned files, interfaces, allowed assumptions and acceptance cases. Separate independent expected-result preparation from implementation where practical. Shared files/contracts require coordination before edits, not competing parallel rewrites.
