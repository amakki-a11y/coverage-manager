using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Thread-safe cache of the latest bid/ask per MT5 symbol. Populated by the
/// <c>OnTick</c> callback during MT5 Manager event dispatch; consumed by the
/// WebSocket broadcast (for flashing price cells) and by downstream
/// views that need a spot reference.
/// </summary>
public class PriceCache
{
    private readonly ConcurrentDictionary<string, PriceQuote> _prices = new();

    public void Update(string symbol, decimal bid, decimal ask)
    {
        _prices[symbol] = new PriceQuote
        {
            Symbol = symbol,
            Bid = bid,
            Ask = ask,
            Timestamp = DateTime.UtcNow
        };
    }

    public PriceQuote? Get(string symbol) =>
        _prices.TryGetValue(symbol, out var q) ? q : null;

    public IReadOnlyList<PriceQuote> GetAll() =>
        _prices.Values.ToList().AsReadOnly();
}
