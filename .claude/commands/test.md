---
argument-hint: File path to generate tests for
---

Generate tests for $ARGUMENTS:

1. Read the file and identify all testable functions/methods
2. For each function, write tests covering:
   - **Happy path** — expected inputs produce expected outputs
   - **Edge cases** — empty, null, undefined, boundaries
   - **Error cases** — invalid inputs, thrown exceptions
3. Use the project's test framework:
   - C# (.cs): MSTest (existing tests in CoverageManager.Tests)
   - TypeScript/React: Vitest
   - Python: pytest
4. Place test files in the appropriate test directory
5. Run tests after creating to verify they pass
