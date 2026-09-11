using System.Globalization;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Turns a <see cref="DealModification"/> the <see cref="DealStore"/> recorded into audit rows, one per field that
/// changed, with the fields and formats the previous Supabase-side comparison used. No read of the durable store is
/// needed: the copy held in memory is what the store had.
/// </summary>
public static class DealChangeAudit
{
    public static IReadOnlyList<TradeAuditEntry> Entries(DealModification m, string source)
    {
        var entries = new List<TradeAuditEntry>();
        var c = CultureInfo.InvariantCulture;
        void Check(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            entries.Add(new TradeAuditEntry
            {
                Source = source,
                DealId = (long)m.After.DealId,
                PositionId = m.After.PositionId == 0 ? null : (long?)m.After.PositionId,
                Login = (long)m.After.Login,
                Symbol = m.After.Symbol,
                FieldChanged = field,
                OldValue = oldValue,
                NewValue = newValue,
                ChangeType = "modified",
                DetectedAt = m.DetectedAtUtc,
            });
        }
        Check("price", m.Before.Price.ToString("F5", c), m.After.Price.ToString("F5", c));
        Check("volume", m.Before.VolumeLots.ToString("F2", c), m.After.VolumeLots.ToString("F2", c));
        Check("profit", m.Before.Profit.ToString("F2", c), m.After.Profit.ToString("F2", c));
        Check("commission", m.Before.Commission.ToString("F2", c), m.After.Commission.ToString("F2", c));
        Check("swap", m.Before.Swap.ToString("F2", c), m.After.Swap.ToString("F2", c));
        Check("fee", m.Before.Fee.ToString("F2", c), m.After.Fee.ToString("F2", c));
        Check("direction", m.Before.Direction, m.After.Direction);
        Check("entry", m.Before.Entry.ToString(c), m.After.Entry.ToString(c));
        return entries;
    }
}
