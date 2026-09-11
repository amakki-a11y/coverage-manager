using System.Globalization;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Remembers what each trading account looked like when it was last written to the durable store, so the periodic
/// account sync writes only the accounts that changed. At feed scale (26,000+ accounts) a full write every cycle is
/// what overloaded Supabase; with this tracker a cycle writes the few thousand accounts whose balance, credit,
/// equity, margin or roster fields moved, and nothing when nothing moved. The first cycle after a start writes all.
/// Not thread-safe: used from the connection's own loop.
/// </summary>
public sealed class AccountSyncTracker
{
    private readonly Dictionary<long, string> _synced = new();

    /// <summary>Accounts written so far (distinct logins).</summary>
    public int SyncedCount => _synced.Count;

    /// <summary>The accounts whose durable-store fields differ from the last written copy (or were never written).</summary>
    public IReadOnlyList<TradingAccount> Changed(IEnumerable<TradingAccount> accounts)
    {
        var changed = new List<TradingAccount>();
        foreach (var a in accounts)
        {
            if (_synced.TryGetValue(a.Login, out var last) && last == Fingerprint(a)) continue;
            changed.Add(a);
        }
        return changed;
    }

    /// <summary>Records the written copies; call after the durable store accepted them.</summary>
    public void MarkSynced(IEnumerable<TradingAccount> accounts)
    {
        foreach (var a in accounts) _synced[a.Login] = Fingerprint(a);
    }

    /// <summary>Forget everything: the next cycle writes every account again.</summary>
    public void Reset() => _synced.Clear();

    /// <summary>
    /// The fields the durable store holds, money to the cent. Excludes the sync timestamps, which change every cycle by
    /// definition, so an unchanged account produces the same fingerprint cycle after cycle.
    /// </summary>
    public static string Fingerprint(TradingAccount a) => string.Join('|',
        a.Source, a.Login.ToString(CultureInfo.InvariantCulture), a.Name, a.GroupName,
        a.Leverage.ToString(CultureInfo.InvariantCulture), a.Currency, a.Status, a.Comment,
        Money(a.Balance), Money(a.Credit), Money(a.Equity), Money(a.Margin), Money(a.FreeMargin),
        a.RegistrationTime?.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) ?? "",
        a.LastTradeTime?.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) ?? "");

    private static string Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
}
