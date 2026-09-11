namespace CoverageManager.Connector.LiveBridge;

/// <summary>What applying a record did to the book.</summary>
public enum FeedApply
{
    Unchanged,
    Added,
    Updated,
    Deleted,
}

/// <summary>
/// The consumer's in-memory book, fed by the feed and read by the IMT5Api queries: open positions by position number,
/// accounts by login, the last tick per symbol, and the deals received (bounded by a retention window). Every apply is
/// idempotent by identity: the same record twice is <see cref="FeedApply.Unchanged"/>; a position update and an account
/// frame replace the whole record (full state, never a difference).
/// </summary>
public sealed class FeedBook
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, RawPosition> _positions = new();
    private readonly Dictionary<ulong, RawAccount> _accounts = new();
    private readonly Dictionary<string, RawTick> _ticks = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, RawDeal> _deals = new();
    private HashSet<ulong>? _snapshotSeen;

    public int PositionCount { get { lock (_gate) return _positions.Count; } }
    public int AccountCount { get { lock (_gate) return _accounts.Count; } }
    public int SymbolCount { get { lock (_gate) return _ticks.Count; } }
    public int DealCount { get { lock (_gate) return _deals.Count; } }

    // ---- apply --------------------------------------------------------------------------

    public FeedApply ApplyPosition(string action, RawPosition position)
    {
        lock (_gate)
        {
            if (action == FeedActions.Delete)
                return _positions.Remove(position.PositionId) ? FeedApply.Deleted : FeedApply.Unchanged;

            _snapshotSeen?.Add(position.PositionId);
            if (_positions.TryGetValue(position.PositionId, out var before))
            {
                if (before == position) return FeedApply.Unchanged;
                _positions[position.PositionId] = position;
                return FeedApply.Updated;
            }
            _positions[position.PositionId] = position;
            return FeedApply.Added;
        }
    }

    public FeedApply ApplyDeal(RawDeal deal)
    {
        lock (_gate)
        {
            if (_deals.TryGetValue(deal.DealId, out var before))
            {
                if (before == deal) return FeedApply.Unchanged;
                _deals[deal.DealId] = deal;
                return FeedApply.Updated;
            }
            _deals[deal.DealId] = deal;
            return FeedApply.Added;
        }
    }

    public FeedApply ApplyAccount(RawAccount account)
    {
        lock (_gate)
        {
            if (_accounts.TryGetValue(account.Login, out var before))
            {
                if (before == account) return FeedApply.Unchanged;
                _accounts[account.Login] = account;
                return FeedApply.Updated;
            }
            _accounts[account.Login] = account;
            return FeedApply.Added;
        }
    }

    public void ApplyTick(RawTick tick)
    {
        lock (_gate) _ticks[tick.Symbol] = tick;
    }

    // ---- snapshot reconciliation --------------------------------------------------------

    /// <summary>A positions snapshot starts: from now on every position applied is noted as present.</summary>
    public void BeginPositionSnapshot()
    {
        lock (_gate) _snapshotSeen = new HashSet<ulong>();
    }

    /// <summary>The snapshot is complete: positions the book holds that the snapshot did not mention are closed and returned.</summary>
    public List<RawPosition> EndPositionSnapshot()
    {
        lock (_gate)
        {
            var seen = _snapshotSeen;
            _snapshotSeen = null;
            if (seen is null) return new List<RawPosition>();
            var gone = _positions.Values.Where(p => !seen.Contains(p.PositionId)).ToList();
            foreach (var p in gone) _positions.Remove(p.PositionId);
            return gone;
        }
    }

    public bool PositionSnapshotActive { get { lock (_gate) return _snapshotSeen is not null; } }

    // ---- queries ------------------------------------------------------------------------

    public List<RawPosition> Positions(ulong login)
    {
        lock (_gate) return _positions.Values.Where(p => p.Login == login).OrderBy(p => p.PositionId).ToList();
    }

    public ulong[] Logins(string? groupMask)
    {
        lock (_gate)
            return _accounts.Values
                .Where(a => GroupMask.Matches(groupMask, a.Group))
                .Select(a => a.Login)
                .OrderBy(l => l)
                .ToArray();
    }

    public RawAccount? Account(ulong login)
    {
        lock (_gate) return _accounts.TryGetValue(login, out var a) ? a : null;
    }

    public RawTick? Tick(string symbol)
    {
        lock (_gate) return _ticks.TryGetValue(symbol, out var t) ? t : null;
    }

    /// <summary>Deals of a login whose time (source clock, milliseconds) lies in [from, to], oldest first.</summary>
    public List<RawDeal> Deals(ulong login, long fromMsc, long toMsc)
    {
        lock (_gate)
            return _deals.Values
                .Where(d => d.Login == login && d.TimeMsc >= fromMsc && d.TimeMsc <= toMsc)
                .OrderBy(d => d.TimeMsc).ThenBy(d => d.DealId)
                .ToList();
    }

    /// <summary>Time (source clock, milliseconds) of the oldest deal held, null when none.</summary>
    public long? EarliestDealMsc()
    {
        lock (_gate) return _deals.Count == 0 ? null : _deals.Values.Min(d => d.TimeMsc);
    }

    /// <summary>Forgets deals older than the given time (source clock, milliseconds); returns how many.</summary>
    public int PruneDeals(long olderThanMsc)
    {
        lock (_gate)
        {
            var old = _deals.Values.Where(d => d.TimeMsc < olderThanMsc).Select(d => d.DealId).ToList();
            foreach (var id in old) _deals.Remove(id);
            return old.Count;
        }
    }
}
