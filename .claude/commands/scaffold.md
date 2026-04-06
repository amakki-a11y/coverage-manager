---
argument-hint: What to scaffold (e.g., "api controller", "react component", "model")
---

Generate boilerplate for: $ARGUMENTS

Detect the project's conventions by reading:
- Existing similar files in the codebase
- CLAUDE.md conventions section

Then create the file(s) matching the project's existing patterns:
- Same naming convention
- Same file structure
- Same imports and patterns used elsewhere
- Include error handling per project rules

Common scaffold types:
- "api controller" — C# controller + route + validation
- "react component" — component + types + hook if needed
- "model" — C# model in CoverageManager.Core/Models
- "service" — C# service in CoverageManager.Api/Services
- "hook" — React custom hook in web/src/hooks
- "page" — React page in web/src/pages

Show the generated files and ask for confirmation before writing.
