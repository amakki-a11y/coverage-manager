using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Connector;

/// <summary>
/// Simulates MT5 Manager API for development/testing.
/// Generates random positions and tick updates.
/// Replace with real MT5Connection when DLLs are available.
/// </summary>
public class MockMT5Connection : BackgroundService
{
    private readonly PositionManager _positionManager;
    private readonly PriceCache _priceCache;
    private readonly ILogger<MockMT5Connection> _logger;
    private readonly Random _rng = new();
    private readonly Action? _onUpdate;

    private static readonly string[] Symbols = ["XAUUSD", "EURUSD", "GBPUSD", "USDJPY", "US30.Z5"];

    private static readonly Dictionary<string, (decimal baseBid, int digits)> SymbolPrices = new()
    {
        ["XAUUSD"] = (2650.00m, 2),
        ["EURUSD"] = (1.08500m, 5),
        ["GBPUSD"] = (1.26800m, 5),
        ["USDJPY"] = (151.500m, 3),
        ["US30.Z5"] = (42500.00m, 2),
    };

    public MockMT5Connection(
        PositionManager positionManager,
        PriceCache priceCache,
        ILogger<MockMT5Connection> logger,
        Action? onUpdate = null)
    {
        _positionManager = positionManager;
        _priceCache = priceCache;
        _logger = logger;
        _onUpdate = onUpdate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MockMT5Connection started — generating simulated B-Book data");

        // Generate initial positions
        GenerateInitialPositions();

        var tickCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Update prices every 100ms
            UpdatePrices();
            tickCount++;

            // Randomly add/remove positions every ~5 seconds
            if (tickCount % 50 == 0)
            {
                if (_rng.NextDouble() > 0.5)
                    AddRandomPosition();
                else
                    RemoveRandomPosition();
            }

            // Update P&L on existing positions based on current prices
            UpdatePositionPnL();

            _onUpdate?.Invoke();

            await Task.Delay(100, stoppingToken);
        }
    }

    private void GenerateInitialPositions()
    {
        ulong[] logins = [1001, 1002, 1003, 1004, 1005];

        for (var i = 0; i < 15; i++)
        {
            var symbol = Symbols[_rng.Next(Symbols.Length)];
            var login = logins[_rng.Next(logins.Length)];
            var direction = _rng.NextDouble() > 0.5 ? "BUY" : "SELL";
            var volume = Math.Round((decimal)(_rng.NextDouble() * 5 + 0.1), 2);
            var (baseBid, _) = SymbolPrices[symbol];
            var priceOffset = baseBid * (decimal)(_rng.NextDouble() * 0.002 - 0.001);
            var ticket = (ulong)(100000 + i);

            var pos = new Position
            {
                Source = "bbook",
                Login = login,
                Symbol = symbol,
                Direction = direction,
                VolumeLots = volume,
                OpenPrice = baseBid + priceOffset,
                CurrentPrice = baseBid,
                Profit = 0,
                Swap = Math.Round((decimal)(_rng.NextDouble() * 10 - 5), 2),
                OpenTime = DateTime.UtcNow.AddHours(-_rng.Next(1, 48)),
                UpdatedAt = DateTime.UtcNow
            };

            _positionManager.UpdateBBookPosition($"bbook:{login}:{ticket}", pos);
        }

        _logger.LogInformation("Generated 15 initial B-Book positions");
    }

    private void UpdatePrices()
    {
        foreach (var (symbol, (baseBid, digits)) in SymbolPrices)
        {
            var variation = baseBid * (decimal)(_rng.NextDouble() * 0.0002 - 0.0001);
            var bid = Math.Round(baseBid + variation, digits);
            var spreadPips = symbol == "XAUUSD" ? 0.30m
                : symbol.Contains("JPY") ? 0.015m
                : symbol == "US30.Z5" ? 2.0m
                : 0.00012m;
            var ask = bid + spreadPips;

            _priceCache.Update(symbol, bid, ask);
        }
    }

    private void UpdatePositionPnL()
    {
        var positions = _positionManager.GetBBookPositions();
        foreach (var pos in positions)
        {
            var quote = _priceCache.Get(pos.Symbol);
            if (quote == null) continue;

            pos.CurrentPrice = pos.Direction == "BUY" ? quote.Bid : quote.Ask;

            if (SymbolPrices.TryGetValue(pos.Symbol, out var info))
            {
                var priceDiff = pos.Direction == "BUY"
                    ? pos.CurrentPrice - pos.OpenPrice
                    : pos.OpenPrice - pos.CurrentPrice;

                // Simplified P&L: price_diff × volume × contract_size
                var contractSize = pos.Symbol switch
                {
                    "XAUUSD" => 100m,
                    "US30.Z5" => 1m,
                    _ => 100000m
                };

                pos.Profit = Math.Round(priceDiff * pos.VolumeLots * contractSize, 2);
            }

            pos.UpdatedAt = DateTime.UtcNow;
        }
    }

    private void AddRandomPosition()
    {
        var symbol = Symbols[_rng.Next(Symbols.Length)];
        ulong login = (ulong)(1001 + _rng.Next(5));
        var direction = _rng.NextDouble() > 0.5 ? "BUY" : "SELL";
        var volume = Math.Round((decimal)(_rng.NextDouble() * 3 + 0.1), 2);
        var (baseBid, _) = SymbolPrices[symbol];
        var ticket = (ulong)(200000 + _rng.Next(100000));

        var pos = new Position
        {
            Source = "bbook",
            Login = login,
            Symbol = symbol,
            Direction = direction,
            VolumeLots = volume,
            OpenPrice = baseBid,
            CurrentPrice = baseBid,
            Profit = 0,
            OpenTime = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _positionManager.UpdateBBookPosition($"bbook:{login}:{ticket}", pos);
        _logger.LogDebug("Added mock position: {Symbol} {Direction} {Volume} lots", symbol, direction, volume);
    }

    private void RemoveRandomPosition()
    {
        var positions = _positionManager.GetBBookPositions();
        if (positions.Count == 0) return;

        // We can't easily get the key, so we skip removal in mock
        // In real MT5, the deal callback provides the ticket
    }
}
