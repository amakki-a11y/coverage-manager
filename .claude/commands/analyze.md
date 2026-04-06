---
argument-hint: Optional — directory path to analyze (defaults to project root)
---

Full codebase analysis on $ARGUMENTS (or project root if not specified).
Do NOT modify any files. Read-only scan.

Report:

1. **Tech Stack** — languages, frameworks, libraries, package manager
2. **Architecture** — folder structure pattern, data flow
3. **Size** — total files, lines of code, number of modules/components
4. **Dependencies** — count, outdated packages, known vulnerabilities
5. **Code Health**
   - Test coverage: are there test files? what framework?
   - Linting: is ESLint/Prettier configured?
   - TypeScript: strict mode? any usage?
   - Error handling: consistent pattern or scattered?
6. **Documentation** — README exists? CLAUDE.md? API docs? inline comments?
7. **Git Health** — last commit date, total commits, branch count, uncommitted changes
8. **Potential Issues** — large files (>500 lines), deeply nested directories, hardcoded values

Present as a structured report. End with:
"Top 3 things to fix first: 1. ... 2. ... 3. ..."
