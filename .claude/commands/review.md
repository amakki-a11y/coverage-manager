---
argument-hint: File path or "staged" for git staged changes
---

Perform a code review on $ARGUMENTS. Check for:

1. **Logic errors** — incorrect conditions, off-by-one, race conditions
2. **Edge cases** — null/undefined handling, empty arrays, boundary values
3. **Error handling** — missing try-catch, unhandled promise rejections
4. **Naming** — unclear variable/function names, misleading names
5. **DRY violations** — duplicated logic that should be extracted
6. **Performance** — unnecessary loops, N+1 queries, memory leaks
7. **Security** — see /security for deeper audit
8. **Readability** — overly complex expressions, missing comments on non-obvious logic

For each issue found, show:
- File and line
- Severity: Critical | Warning | Suggestion
- What's wrong and how to fix it

End with: X critical, Y warnings, Z suggestions.
