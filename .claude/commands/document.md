---
argument-hint: File path to document
---

Generate documentation for $ARGUMENTS:

1. **File overview** — what this file does in 1-2 sentences
2. **Exports** — list all exported functions/classes/constants
3. **For each function/method:**
   - Purpose (one line)
   - Parameters with types
   - Return value with type
   - Example usage
4. **Dependencies** — what this file imports and why

Write as XML doc comments (C#), JSDoc (TypeScript), or docstrings (Python).
Insert directly into the source file. Do NOT create a separate doc file.
