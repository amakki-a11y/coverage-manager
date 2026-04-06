Generate a conventional commit message for staged changes:

1. Run `git diff --staged`
2. Generate message:
   - `feat:` new feature
   - `fix:` bug fix
   - `docs:` documentation
   - `refactor:` restructuring
   - `test:` adding tests
   - `chore:` maintenance
   - `style:` formatting only
3. Subject under 72 characters
4. Add body if change is complex

Show the message and ask for confirmation. Do NOT commit automatically.
