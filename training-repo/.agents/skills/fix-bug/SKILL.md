---
name: fix-bug
description: Use only when explicitly invoked to fix one OrderHub defect through reproduction, diagnosis, a minimal source fix, regression testing, and review.
---

# Fix one OrderHub bug

Handle exactly one defect per invocation.

1. Restate the observable symptom and identify a deterministic reproduction.
2. Trace the request from controller to service and repository as applicable.
3. Explain the root cause with file and line evidence before editing.
4. Add a regression test that exercises the user-visible failure path and
   confirm it fails for the expected reason when practical.
5. Apply the smallest source fix. Do not mix cleanup, dependency upgrades, or
   unrelated refactoring into the change.
6. Run the targeted test and then the complete solution test suite.
7. Review the diff for OrderHub layering, validation, query behavior, and
   accidental generated-file changes.
8. Present the diff and verification results for user review. Do not push.
   Commit only when the user explicitly requests it.
