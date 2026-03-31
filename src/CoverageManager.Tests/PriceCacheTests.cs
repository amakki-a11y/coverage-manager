using CoverageManager.Core.Engines;

namespace CoverageManager.Tests;

[TestClass]
public class PriceCacheTests
{
    [TestMethod]
    public void Update_And_Get_ReturnsLatestPrice()
    {
        var cache = new PriceCache();
        cache.Update("XAUUSD", 2650.50m, 2650.80m);

        var quote = cache.Get("XAUUSD");
        Assert.IsNotNull(quote);
        Assert.AreEqual(2650.50m, quote.Bid);
        Assert.AreEqual(2650.80m, quote.Ask);
        Assert.AreEqual(0.30m, quote.Spread);
    }

    [TestMethod]
    public void Get_NonExistentSymbol_ReturnsNull()
    {
        var cache = new PriceCache();
        Assert.IsNull(cache.Get("NOPE"));
    }

    [TestMethod]
    public void Update_OverwritesPrevious()
    {
        var cache = new PriceCache();
        cache.Update("EURUSD", 1.08500m, 1.08520m);
        cache.Update("EURUSD", 1.08600m, 1.08620m);

        var quote = cache.Get("EURUSD");
        Assert.IsNotNull(quote);
        Assert.AreEqual(1.08600m, quote.Bid);
    }

    [TestMethod]
    public void GetAll_ReturnsAllSymbols()
    {
        var cache = new PriceCache();
        cache.Update("XAUUSD", 2650, 2651);
        cache.Update("EURUSD", 1.085m, 1.086m);
        cache.Update("GBPUSD", 1.268m, 1.269m);

        var all = cache.GetAll();
        Assert.AreEqual(3, all.Count);
    }

    [TestMethod]
    public void ConcurrentUpdates_NoExceptions()
    {
        var cache = new PriceCache();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var bid = 2650m + i;
            tasks.Add(Task.Run(() => cache.Update("XAUUSD", bid, bid + 0.3m)));
        }

        Task.WaitAll(tasks.ToArray());

        var quote = cache.Get("XAUUSD");
        Assert.IsNotNull(quote);
    }
}
