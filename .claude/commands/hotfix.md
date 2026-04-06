---
argument-hint: Description of the bug to fix
---

Emergency hotfix workflow for: $ARGUMENTS

1. Create a hotfix branch: `git checkout -b hotfix/[short-description]`
2. Identify the root cause — search codebase for related code
3. Write a failing test that reproduces the bug
4. Implement the minimal fix
5. Run all tests to ensure no regressions
6. Run /security on changed files
7. Generate commit message: `fix: [description]`
8. Show summary: what broke, why, what was changed

Ask: "Ready to commit and merge back to main?"
