# Persistence agent instructions

Read architecture, domain revision semantics, API replay and recovery workflow. Own transactional accepted state, revision control, outbox, job records, asset manifests, snapshots, backups and migrations.

Commit domain changes/audit/invalidation as one unit. Stage and verify immutable assets before references become accepted. Ensure retries never duplicate accepted operations, materials or releases. Caches/job progress do not rewrite engineering revision history.

Test interrupted writes, missing/corrupt assets, replay after crash, stale jobs, restore and old snapshot access. Use a consistent live database backup mechanism. Do not assume a copied database plus independently copied files is a consistent project. Hashes are integrity evidence, not reviewer authentication.
