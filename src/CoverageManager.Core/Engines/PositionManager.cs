using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// In-memory position store. Holds both B-Book and coverage positions.
/// Thread-safe for concurrent updates from MT5 callbacks and HTTP posts.
/// </summary>
public class PositionManager
{
    // Key: "bbook:{login}:{ticket}" or "coverage:{ticket}"
    private readonly ConcurrentDictionary<string, Position> _positions = new();
    private readonly ConcurrentDictionary<string, SymbolMapping> _mappings = new();

    public void LoadMappings(IEnumerable<SymbolMapping> mappings)
    {
        _mappings.Clear();
        BridgePipResolver.Overrides.Clear();
        foreach (var m in mappings.Where(m => m.IsActive))
        {
            _mappings[m.BBookSymbol.ToUpperInvariant()] = m;
            _mappings[m.CoverageSymbol.ToUpperInvariant()] = m;

            // Feed explicit pip sizes into the Bridge tab's pip resolver.
            if (m.PipSize is { } pip && pip > 0m)
            {
                BridgePipResolver.Overrides[m.CanonicalName] = pip;
                BridgePipResolver.Overrides[m.BBookSymbol] = pip;
                BridgePipResolver.Overrides[m.CoverageSymbol] = pip;
            }
        }
    }

    public SymbolMapping? FindMapping(string symbol, string source)
    {
        var key = symbol.ToUpperInvariant();
        if (_mappings.TryGetValue(key, out var mapping))
            return mapping;
        return null;
    }

    public IReadOnlyDictionary<string, SymbolMapping> GetAllMappings() =>
        _mappings;

    public void UpdateBBookPosition(string key, Position position)
    {
        var mapping = FindMapping(position.Symbol, "bbook");
        if (mapping != null)
        {
            position.CanonicalSymbol = mapping.CanonicalName;
            position.VolumeNormalized = position.VolumeLots; // already in B-Book lots
        }
        else
        {
            position.CanonicalSymbol = position.Symbol;
            position.VolumeNormalized = position.VolumeLots;
        }
        _positions[key] = position;
    }

    public void UpdateCoveragePositions(IEnumerable<CoveragePositionDto> dtos)
    {
        // Remove old coverage positions
        var coverageKeys = _positions.Keys.Where(k => k.StartsWith("coverage:")).ToList();
        foreach (var k in coverageKeys)
            _positions.TryRemove(k, out _);

        // Add fresh
        foreach (var dto in dtos)
        {
            var mapping = FindMapping(dto.Symbol, "coverage");
            var pos = new Position
            {
                Source = "coverage",
                Symbol = dto.Symbol,
                CanonicalSymbol = mapping?.CanonicalName ?? dto.Symbol,
                Direction = dto.Direction,
                VolumeLots = dto.Volume,
                VolumeNormalized = mapping?.NormalizeCoverageVolume(dto.Volume) ?? dto.Volume,
                OpenPrice = dto.OpenPrice,
                CurrentPrice = dto.CurrentPrice,
                Profit = dto.Profit,
                Swap = dto.Swap,
                Login = (ulong)dto.Login,
                OpenTime = dto.OpenTime ?? DateTime.MinValue,
                UpdatedAt = DateTime.UtcNow
            };
            _positions[$"coverage:{dto.Ticket}"] = pos;
        }
    }

    /// <summary>
    /// Replace all B-Book positions atomically. Removes closed positions, adds/updates open ones.
    /// </summary>
    public void SnapshotBBookPositions(Dictionary<string, Position> snapshot)
    {
        // Remove old bbook positions not in the new snapshot
        var bbookKeys = _positions.Keys.Where(k => k.StartsWith("bbook:")).ToList();
        foreach (var k in bbookKeys)
        {
            if (!snapshot.ContainsKey(k))
                _positions.TryRemove(k, out _);
        }

        // Add/update from snapshot (applies mapping)
        foreach (var (key, position) in snapshot)
            UpdateBBookPosition(key, position);
    }

    public void RemoveBBookPosition(string key)
    {
        _positions.TryRemove(key, out _);
    }

    public IReadOnlyList<Position> GetAllPositions() =>
        _positions.Values.ToList().AsReadOnly();

    public IReadOnlyList<Position> GetBBookPositions() =>
        _positions.Values.Where(p => p.Source == "bbook").ToList().AsReadOnly();

    public IReadOnlyList<Position> GetCoveragePositions() =>
        _positions.Values.Where(p => p.Source == "coverage").ToList().AsReadOnly();
}
