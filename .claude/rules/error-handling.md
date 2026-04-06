---
paths:
  - "src/**"
  - "collector/**"
  - "web/src/**"
---

## Error Handling Rules
- Always wrap async operations in try-catch
- Never swallow errors — at minimum log with context
- C# backend: return proper HTTP status codes (400 bad input, 401 auth, 500 server)
- Python collector: return JSON error responses with status codes
- React frontend: catch fetch errors, show user-friendly fallback UI
- WebSocket: handle disconnects gracefully with auto-reconnect
- MT5 API: log connection failures with server/login context
- Supabase: handle batch upsert failures per-record, don't fail entire batch
- Never expose internal errors or stack traces to the frontend
- Deal dedup: log conflicts but don't throw on duplicate deal_id
