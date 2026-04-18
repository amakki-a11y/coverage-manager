# Centroid Database Specification

Source: https://bridge.centroidsol.com/docs/517178025
Captured: 2026-04-16

## Summary

Centroid offers **TradesDB + ConfigDB replicas via Postgres** using their own DB replicator. This is a separate paid add-on — NOT the default way to access Centroid data.

## Setup requirements

1. Reporting Server for the database (hosted by Centroid on their infrastructure).
2. Source IP of the servers that will connect to the database (for whitelisting).

## How to request

1. Email `support@centroidsol.com` with subject `"DB Server"`.
2. State that you want access to your trades/config DB and include the source IP.
3. Centroid Support will reply with additional information including the cost.

## Credentials provided by Centroid

1. DB host / IP
2. DB Port
3. DB Name
4. DB Username
5. DB Password

## Decision for Coverage Manager

**We will not use the DB replica in the first iteration.** Reasons:

- Extra monthly cost (unquoted publicly).
- Requires a separate support ticket and provisioning.
- We can persist our own post-trade history to Supabase from the live Dropcopy FIX 4.4 feed — everything we need flows through Execution Reports (tag 37 `OrderID`, tag 55 `Symbol`, tag 54 `Side`, tag 32 `LastQty`, tag 31 `LastPx`, tag 60 `TransactTime`, tag 90002 `Group`).
- Supabase gives us the queryability we need for the Bridge tab's historical filter.

We can revisit this if/when we need pre-existing history from before the Coverage Manager was connected.
