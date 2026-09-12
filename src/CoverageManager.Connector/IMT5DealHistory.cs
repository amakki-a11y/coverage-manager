using CoverageManager.Core.Models;

namespace CoverageManager.Connector;

/// <summary>
/// Optional, implemented by an <see cref="IMT5Api"/> whose <see cref="IMT5Api.RequestDeals"/> cannot answer from the
/// server's whole history (the Live Bridge feed: only the deals received since its resume point, for a retention
/// window). <see cref="MT5ManagerConnection.DealHistory"/> reports <see cref="DealHistoryWindow.Full"/> for a provider
/// without it (the Manager API), and every consumer that compares provider deals with stored deals - the nightly
/// reconciliation, <c>/api/exposure/verify</c>, the deal reloads - limits itself to the window reported here.
/// </summary>
public interface IMT5DealHistory
{
    DealHistoryWindow DealHistory { get; }
}
