# Centroid FIX 4.4 DropCopy API — Specification v1.1

Source: https://bridge.centroidsol.com/docs/517178093
Captured: 2026-04-16

## Changelog

| Date | Version | Change |
|---|---|---|
| 2026-01-09 | 1.0.4 | Execution Report — added tag 90016 (Taker Name) |
| 2025-08-22 | 1.0.3 | Execution Report — added tag 90015 (Maker Name) |
| 2021-02-08 | 1.0.2 | Execution Report — added available external information |
| 2021-08-13 | 1.0.1 | Enhanced Execution Report |

## Important notes (verbatim from spec)

- All time formats are **UTC**.
- Session timings are agreed with the broker.
- **IP whitelisting is required** to connect to the live server.
- Timestamp precision is 3+ digits (ms or μs).
- Trading session resets sequence number on logon.
- Giveup rule must be configured on the Centroid side.
- Custom tags in Execution Reports must be enabled by Centroid Support on request.

## Supported messages

### Session-level (admin)

| Message | MsgType (35) | Direction |
|---|---|---|
| Heartbeat | `0` | Both |
| Test Request | `1` | Both |
| Logon | `A` | Client → Centroid |
| Logout | `5` | Both |
| Resend Request | `2` | Both |
| Reject | `3` | Both |
| Business Reject | `j` | Both |
| Sequence Reset | `4` | Centroid → Client |

### Application-level

| Message | MsgType (35) |
|---|---|
| Execution Report | `8` |

## Standard header

| Tag | Field | Req | Notes |
|---|---|---|---|
| 8 | BeginString | Y | `FIX.4.4` |
| 9 | BodyLength | Y | |
| 35 | MsgType | Y | |
| 49 | SenderCompID | Y | Provided by Centroid |
| 56 | TargetCompID | Y | Provided by Centroid (e.g. `CENTROID_SOL`) |
| 34 | MsgSeqNum | Y | |
| 50 | SenderSubID | N | Optional |
| 52 | SendingTime | Y | UTC |

## Logon (MsgType=A)

| Tag | Field | Req | Notes |
|---|---|---|---|
| 98 | EncryptMethod | Y | `0` (no encryption) |
| 108 | HeartBtInt | Y | Heartbeat interval seconds (commonly 30) |
| 141 | ResetSeqNumFlag | N | Usually `Y` — reset on logon |
| 553 | Username | Y | |
| 554 | Password | Y | |

Example:
```
8=FIX.4.4 | 9=128 | 35=A | 34=1 | 49=DC_Centroid | 52=20210720-14:31:59.703 |
56=CENTROID_SOL | 98=0 | 108=30 | 141=Y | 553=Username | 554=Password | 10=140
```

## Execution Report (MsgType=8) — the one that matters

| Tag | Field | Req | Description |
|---|---|---|---|
| 1 | Account | N | Trading account name |
| 11 | ClOrdID | Y | Unique client-side order ID |
| 17 | ExecID | Y | Unique exec ID assigned by Centroid |
| 37 | **OrderID** | Y | **Centroid Gateway order ID. This is the "Cen Ord ID" we correlate on.** |
| 150 | ExecType | Y | `2 = Filled` |
| 39 | OrdStatus | Y | `1 = Partially filled`, `2 = Filled` |
| 55 | Symbol | Y | e.g. `EURUSD` |
| 54 | Side | Y | `1 = Buy`, `2 = Sell` |
| 40 | OrdType | Y | `1 = Market`, `2 = Limit`, `3 = Stop` |
| 59 | TimeInForce | Y | `1 = GTC`, `3 = IOC`, `4 = FOK` |
| 38 | OrderQty | Y | |
| 32 | LastQty | C | Fill quantity |
| 14 | CumQty | C | Cumulative quantity executed |
| 151 | LeavesQty | C | Remaining quantity open |
| 31 | LastPx | C | Price of this fill |
| 6 | AvgPx | C | VWAP of executed quantity |
| 44 | Price | N | Limit order price |
| 58 | Text | Y | Free text |
| 60 | TransactTime | Y | UTC transaction time |

### Custom tags (enable via support)

| Tag | Field | Description |
|---|---|---|
| 90001 | Login | External MT4/MT5 login |
| 90002 | **Group** | External MT4/MT5 group — **used to classify CLIENT vs COV OUT** (e.g. `real\test\a-book` vs `...\b-book`) |
| 90003 | Order | External MT4/MT5 ticket |
| 90006 | Position ID | External position number |
| 90010 | B Filled | B-Book filled volume |
| 90011 | External Markup | Markup added by the taker |
| 90015 | **Maker Name** | Name of the maker that executed the trade |
| 90016 | **Taker Name** | Name of the taker that placed the trade (requires explicit enablement on Centroid side) |
| 132 | Bid Price | External bid at time of fill |
| 133 | Offer Price | External ask at time of fill |

## Example — Fully filled EURUSD with custom tags

```
8=FIX.4.4 | 9=411 | 35=8 | 34=16 | 49=CENTROID_SOL |
52=20220208-08:45:16.151684 | 56=DC_Centroid_DropCopy | 1=test_tem |
6=1.14029000 | 11=32867651006464-12 | 14=1000.00000000 | 17=3566625-12 |
31=1.14029000 | 32=1000.00000000 | 37=3566625-12 | 38=1000.00000000 |
39=2 | 40=1 | 54=2 | 55=EURUSD | 58=Execution | 59=4 |
60=20220208-08:45:16 | 132=1.14023000 | 133=1.14035000 | 150=2 |
151=0.00000000 | 90001=1014 | 90002=real\test\a-book | 90003=2970554 |
90006=2970553 | 90011=-0.00006000 | 10=122
```

Key things to pull out of a single Execution Report:
- `37=3566625-12` → OrderID (correlation key for CLIENT + COV OUT legs)
- `55=EURUSD`, `54=2` (SELL), `32=1000` (volume), `31=1.14029` (price), `60=20220208-08:45:16` (UTC)
- `90002=real\test\a-book` → this fill is a coverage (LP) leg → classify as `COV_OUT`

## Session-level example — Logon round-trip

```
[in]  8=FIX.4.4 | 35=A | 34=1 | 49=DC_Centroid | 52=20210720-14:31:59.703 |
      56=CENTROID_SOL | 98=0 | 108=30 | 141=Y | 553=Username | 554=Password | 10=140
[out] 8=FIX.4.4 | 35=A | 34=1 | 49=CENTROID_SOL | 52=20210720-14:31:59.707021 |
      56=DC_Centroid | 98=0 | 108=30 | 141=Y | 10=026
```

## Normalization to `BridgeDeal` (our internal shape)

```
BridgeDeal {
  dealId       = $ExecID (tag 17)
  cenOrdId     = $OrderID (tag 37)
  symbol       = $Symbol (tag 55), upper-cased
  source       = classify($Group tag 90002) → CLIENT | COV_OUT
  side         = $Side (tag 54): 1→BUY, 2→SELL
  volume       = $LastQty (tag 32)
  price        = $LastPx (tag 31)
  timeUtc      = $TransactTime (tag 60) + ms from $SendingTime (tag 52)
  lpName       = $MakerName (tag 90015, optional)
  takerName    = $TakerName (tag 90016, optional)
  mtLogin      = $Login (tag 90001, optional)
  mtTicket     = $Order (tag 90003, optional)
  positionId   = $PositionID (tag 90006, optional)
  externalMarkup = $ExternalMarkup (tag 90011, optional)
  bidAtFill    = $BidPrice (tag 132, optional)
  askAtFill    = $OfferPrice (tag 133, optional)
}
```

## Classification rule (configurable)

By default:
- Group matches regex `\\a-book(\\|$)` → `COV_OUT`
- Group matches regex `\\b-book(\\|$)` → `CLIENT`
- Otherwise → `UNCLASSIFIED` (logged, included as CLIENT by default for safety)

Override via `Centroid:GroupClassification` in `appsettings.json`.
