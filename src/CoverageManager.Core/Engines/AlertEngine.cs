using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

public class AlertEngine
{
    private readonly ExposureEngine _exposureEngine;
    private readonly PositionManager _positionManager;
    private readonly ConcurrentDictionary<Guid, AlertEvent> _activeAlerts = new();
    private volatile List<RiskThreshold> _thresholds = new();

    // Track which thresholds are currently breached to avoid duplicate alerts
    private readonly ConcurrentDictionary<string, DateTime> _breachedKeys = new();
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);

    public AlertEngine(ExposureEngine exposureEngine, PositionManager positionManager)
    {
        _exposureEngine = exposureEngine;
        _positionManager = positionManager;
    }

    public void LoadThresholds(IEnumerable<RiskThreshold> thresholds)
    {
        _thresholds = thresholds.Where(t => t.Enabled).ToList();
    }

    /// <summary>
    /// Evaluate all thresholds against current exposure. Returns newly fired alerts.
    /// </summary>
    public List<AlertEvent> Evaluate()
    {
        var newAlerts = new List<AlertEvent>();
        if (_thresholds.Count == 0) return newAlerts;

        var exposure = _exposureEngine.CalculateExposure();
        var positions = _positionManager.GetAllPositions();

        foreach (var threshold in _thresholds)
        {
            var matchingSymbols = string.IsNullOrEmpty(threshold.Symbol)
                ? exposure // empty symbol = all symbols
                : exposure.Where(e => e.CanonicalSymbol.Equals(threshold.Symbol, StringComparison.OrdinalIgnoreCase)
                    || e.CanonicalSymbol.Replace("-", "").Replace(".", "").Equals(
                        threshold.Symbol.Replace("-", "").Replace(".", ""), StringComparison.OrdinalIgnoreCase));

            foreach (var exp in matchingSymbols)
            {
                var (breached, actualValue, message) = EvaluateThreshold(threshold, exp, positions);
                if (!breached) continue;

                var key = $"{threshold.Id}:{exp.CanonicalSymbol}";

                // Check cooldown — don't fire same alert repeatedly
                if (_breachedKeys.TryGetValue(key, out var lastFired) &&
                    DateTime.UtcNow - lastFired < CooldownPeriod)
                    continue;

                _breachedKeys[key] = DateTime.UtcNow;

                var alert = new AlertEvent
                {
                    ThresholdId = threshold.Id ?? Guid.Empty,
                    TriggerType = threshold.TriggerType,
                    Symbol = exp.CanonicalSymbol,
                    Severity = threshold.Severity,
                    Message = message,
                    ThresholdValue = threshold.Value,
                    ActualValue = actualValue,
                    TriggeredAt = DateTime.UtcNow,
                };

                _activeAlerts[alert.Id] = alert;
                newAlerts.Add(alert);
            }
        }

        // Clean up old acknowledged alerts (older than 1 hour)
        var cutoff = DateTime.UtcNow.AddHours(-1);
        foreach (var (id, alert) in _activeAlerts)
        {
            if (alert.Acknowledged && alert.AcknowledgedAt < cutoff)
                _activeAlerts.TryRemove(id, out _);
        }

        // Clean up old cooldown entries
        foreach (var (key, time) in _breachedKeys)
        {
            if (DateTime.UtcNow - time > CooldownPeriod * 2)
                _breachedKeys.TryRemove(key, out _);
        }

        return newAlerts;
    }

    private static (bool breached, decimal actualValue, string message) EvaluateThreshold(
        RiskThreshold threshold, ExposureSummary exp, IReadOnlyList<Position> positions)
    {
        decimal actual;
        string desc;

        switch (threshold.TriggerType.ToLowerInvariant())
        {
            case "exposure":
                actual = Math.Abs(exp.NetVolume);
                desc = $"Net exposure {exp.NetVolume:F2} lots (threshold: {threshold.Value:F2})";
                break;

            case "hedge_ratio":
                actual = exp.HedgeRatio;
                desc = $"Hedge ratio {exp.HedgeRatio:F0}% (threshold: {threshold.Value:F0}%)";
                break;

            case "pnl":
                actual = exp.NetPnL;
                desc = $"Net P&L ${exp.NetPnL:F2} (threshold: ${threshold.Value:F2})";
                break;

            case "client_pnl":
                actual = exp.BBookPnL;
                desc = $"Client P&L ${exp.BBookPnL:F2} (threshold: ${threshold.Value:F2})";
                break;

            case "account_exposure":
                // Find the login with the largest position in this symbol
                var symbolPositions = positions
                    .Where(p => p.CanonicalSymbol.Equals(exp.CanonicalSymbol, StringComparison.OrdinalIgnoreCase)
                        && p.Source == "bbook")
                    .GroupBy(p => p.Login)
                    .Select(g => new { Login = g.Key, Volume = g.Sum(p => p.VolumeNormalized) })
                    .OrderByDescending(g => Math.Abs(g.Volume))
                    .FirstOrDefault();

                if (symbolPositions == null)
                    return (false, 0, string.Empty);

                actual = Math.Abs(symbolPositions.Volume);
                desc = $"Account {symbolPositions.Login} exposure {symbolPositions.Volume:F2} lots on {exp.CanonicalSymbol} (threshold: {threshold.Value:F2})";
                break;

            default:
                return (false, 0, string.Empty);
        }

        var breached = threshold.Operator switch
        {
            "gt" => actual > threshold.Value,
            "lt" => actual < threshold.Value,
            "gte" => actual >= threshold.Value,
            "lte" => actual <= threshold.Value,
            _ => false
        };

        return (breached, actual, desc);
    }

    public IReadOnlyList<AlertEvent> GetActiveAlerts() =>
        _activeAlerts.Values
            .OrderByDescending(a => a.TriggeredAt)
            .ToList()
            .AsReadOnly();

    public IReadOnlyList<AlertEvent> GetUnacknowledgedAlerts() =>
        _activeAlerts.Values
            .Where(a => !a.Acknowledged)
            .OrderByDescending(a => a.TriggeredAt)
            .ToList()
            .AsReadOnly();

    public bool AcknowledgeAlert(Guid alertId)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.Acknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public int ActiveAlertCount => _activeAlerts.Count(a => !a.Value.Acknowledged);
}
