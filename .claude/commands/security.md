---
argument-hint: File path, directory, or "all" for full project
---

Security audit on $ARGUMENTS. Check for:

1. **Exposed secrets** — API keys, passwords, tokens in code or config
2. **SQL injection** — raw queries without parameterization
3. **XSS** — unescaped user input rendered in HTML/JSX
4. **Auth holes** — missing auth checks on protected routes
5. **CORS** — overly permissive configuration
6. **Input validation** — missing or weak validation on user inputs
7. **Dependency vulnerabilities** — run npm audit or dotnet list package --vulnerable
8. **File access** — path traversal, unrestricted uploads
9. **Rate limiting** — missing rate limits on public endpoints
10. **Environment** — .env in .gitignore, no secrets in CLAUDE.md

For each issue:
- Severity: Critical | High | Medium
- Location (file:line)
- Fix recommendation
