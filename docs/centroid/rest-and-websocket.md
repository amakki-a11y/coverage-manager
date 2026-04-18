# Centroid CS 360 — REST + WebSocket APIs (v1.7)

Extracted 2026-04-16 from the HTML references in `Downloads/broker_rest_api_reference_rest.html`
and `Downloads/broker_realtime_api_reference_realtime.html`.

**Machine-readable specs saved alongside this file:**
- `cs360-rest-openapi.json`
- `cs360-realtime-asyncapi.json`

## Why this matters

The prior assumption ("Centroid has no REST API — must use FIX 4.4 Dropcopy") was wrong. These
docs describe a full **REST + WebSocket** surface that does exactly what the Bridge tab needs.
Compared to Dropcopy FIX 4.4:

| Concern | FIX 4.4 Dropcopy | REST + WS (this doc) |
|---|---|---|
| Transport | Raw TCP + FIX messages | HTTPS + WSS |
| Auth | Session Logon + whitelisted IP | Username/password → JWT |
| IP whitelist | Required | Not required |
| Dev dependency | QuickFIX/n | Just `HttpClient` + `ClientWebSocket` |
| Payload | Binary-ish pipe-delimited | JSON |
| Test without creds | Impossible (needs IP allow-list) | `curl -X POST` against `/v2/api/login` |

## Servers

- REST base URL: `https://bridge.centroidsol.com`
- WebSocket: `wss://bridge.centroidsol.com/ws?token={token}&q={client_code}`

## Auth

### Signin
- `POST /v2/api/login`
  - Request: `{ "username": string, "password": string }`
  - Response: `{ "expire": string, "token": string, "user": { username, access_type, email, first_name, last_name, user_risk_accounts: [...] } }`
  - `access_type` must be `"api"` or `"both"` — `"ui"` accounts cannot use the API.

### Refresh
- `GET /v1/api/refresh_token` with `Authorization: Bearer <token>`

### Required headers on every data call
- `Authorization: Bearer <token>`
- `x-forward-client: <client_code>`    ← provided by Centroid when they create your API user
- `x-forward-user: <username>`

## Reports — what the Bridge tab uses

### Orders (CLIENT side)
`POST /v1/api/orders_report`

Request (all filters optional, all arrays):
```json
{
  "start_date": 1700000000,          // unix seconds
  "end_date": 1700086400,
  "symbol":   ["EURUSD", "XAUUSD"],
  "account":  [], "login": [], "group": [],
  "cen_ord_id": [], "order": [], "execution": [],
  "risk_account": [], "markup_models": [], "displayed_columns": []
}
```

Response row (trimmed to the fields that matter for the Bridge tab):
```
cen_ord_id          — correlation key to coverage legs (matches MakerOrders.cen_ord_id)
client_ord_id
symbol, party_symbol
side                — int
avg_price, price, req_avg_price
volume, fill_volume, afill_volume, bfill_volume
volume_abook, volume_bbook      — split by book
a_avg_price, b_avg_price        — VWAP per book (A = LP, B = internal)
a_tot_fill_volume, b_tot_fill_volume
total_markup, ext_markup
ext_login, ext_group_raw, ext_order, ext_posid, ext_dealid
recv_time_msc                   — microseconds (UTC)
resp_recv_time_mcs, party_send_time_mcs
maker, maker_cat, node, node_account, fix_session
comment, res_text, res_state
```

### Maker Orders (COV OUT side — individual LP legs)
`POST /v1/api/maker_orders`

Request: same filter shape as `orders_report`.

Response row fields that matter:
```
cen_ord_id             — parent client order (join to orders_report.cen_ord_id)
cen_client_ord_id      — the specific leg id
client_ord_id
symbol, party_symbol
side_value             — string
volume_value, fill_volume_value, tot_fill_volume_value
avg_price, price, raw_avg_price, req_price
notional
contract_size
maker_order_id_value, maker_symbol_value, lpsid
recv_time_value
party_recv_time_mcs_value, party_send_time_mcs_value, send_time_mcs_value
ext_login, ext_group, ext_order, ext_posid, ext_dealid, ext_markup
slippage, req_slippage, price_dev
state, res_state_value, req_state
```

### Position snapshot (ad-hoc)
`POST /v1/api/position` — returns aggregated position rows. Useful for sanity-checking
but not essential to the Bridge tab.

## WebSocket — real-time events

URL: `wss://bridge.centroidsol.com/ws?token={TOKEN}&q={CLIENT_CODE}`

Subscribe pattern: send `{ "key": "<channel>" }` after connect.

Channels relevant to Bridge tab:

### `live_trades` ★ the real-time deal stream
Subscribe: `{ "key": "live_trades" }`

Published payload:
```json
{
  "live_trades": [{
    "cen_ord_id": "…",
    "ext_login": 1014,
    "ext_group": "Cov\\\\70-A2\\\\Cov_Netting",
    "ext_order": 2970554,
    "recv_time_value": "2026-04-16T18:00:03.155Z",
    "node": "…", "node_account": "…",
    "symbol": "EURUSD", "party_symbol": "EURUSD-",
    "side_value": "BUY",
    "ord_type_value": "MARKET",
    "time_in_force_value": "IOC",
    "volume_value": 1000,
    "fill_volume_value": 1000,
    "notional": 1100.29,
    "avg_price": 1.10029,
    "price": 1.10029,
    "fill_state": "FULL",
    "state": "COMPLETED",
    "reason_value": ""
  }]
}
```

### `position`
Subscribe: `{ "key": "position", "value": { "position_account": [...], "symbol": [...], "taker": [...] } }`

Published: `{ "position_report": [ {aavg_price, bavg_price, anet_volume, bnet_volume, net_volume, margin, pl, markup, ...} ], "api_error": false }`

### `account_balance`
Subscribe: `{ "key": "account_balance", "value": { "risk_account": [...] } }`

Published: balances per risk account (equity, margin, pl, notional, etc.).

### Other channels (not currently needed)
`maker_status`, `depth_feeding_status`, `trading_status`, `feeder_status`,
`market_watch`, `subscribe_alert`

## CLIENT vs COV OUT classification

Two ways to tell them apart in the REST / WS responses:

1. **Separate endpoints.** `orders_report` = client-facing orders; `maker_orders` = LP legs
   routed out. Already split at the source, no regex needed.
2. **Inside a single row.** `orders_report` rows carry `volume_abook` / `volume_bbook` and
   `a_avg_price` / `b_avg_price` — the internal vs LP split for that one client order.

For the Bridge tab we use both: `live_trades` + `orders_report` give us the client fills,
`maker_orders` gives us the coverage legs, and we join them on `cen_ord_id`.

## Testing without building anything

The FIX path could not be tested without IP whitelist + full client. This can:

```bash
curl -sS -X POST https://bridge.centroidsol.com/v2/api/login \
  -H "Content-Type: application/json" \
  -d '{"username":"YOUR_USER","password":"YOUR_PASS"}'
```

- 200 → JSON with `token`, `expire`, `user`. Creds work.
- 401 → `{"message":"Unauthorized"}`. Creds wrong.
- DNS/TLS error → network or URL issue.

Then to verify the token:
```bash
curl -sS https://bridge.centroidsol.com/v1/api/refresh_token \
  -H "Authorization: Bearer $TOKEN" \
  -H "x-forward-client: $CLIENT_CODE" \
  -H "x-forward-user: $USERNAME"
```

Once this works, the rest of the integration is straightforward.
