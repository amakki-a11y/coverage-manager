---
argument-hint: Task ID to mark as complete
---

Quality gate before marking task $ARGUMENTS as done. Run ALL checks:

1. **Tests pass** — run `dotnet test CoverageManager.sln`
2. **No lint errors** — run the project's lint command if configured
3. **No console.logs** — grep for console.log/console.debug in changed files
4. **No TODO/FIXME** — grep for TODO and FIXME in changed files
5. **Code documented** — changed functions have doc comments
6. **Commit ready** — all changes staged, commit message prepared

Results:
- Tests: pass/fail
- Lint: pass/fail
- No console.logs: pass/fail
- No TODOs: pass/X found
- Documented: pass/fail
- Commit ready: pass/fail

If ALL pass: mark the task as done and commit.
If ANY fail: list what needs fixing. Do NOT mark as done.
