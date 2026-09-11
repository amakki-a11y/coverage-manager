namespace CoverageManager.Core.Models;

/// <summary>
/// How far back the active MT5 provider's deal history reaches: from when on "the provider does not return this deal"
/// means "this deal is not on the server".
/// <list type="bullet">
/// <item><see cref="Full"/>: the Manager API answers a deal request from the server's whole history.</item>
/// <item><see cref="Since"/>: the Live Bridge feed holds only the deals it received since its resume point, for a
/// retention window; <see cref="FromUtc"/> is the earliest instant its record is complete from, never earlier than the
/// earliest deal it holds. A stored deal older than that which the feed lacks proves nothing and must never be treated
/// as a ghost.</item>
/// <item><see cref="None"/>: the provider holds no deals yet; nothing can be compared and nothing may be deleted.</item>
/// </list>
/// The nightly reconciliation sweep, <c>/api/exposure/verify</c> and the deal reloads clamp their work to this window
/// through <see cref="Engines.DealReconciler"/>.
/// </summary>
public readonly record struct DealHistoryWindow(bool Complete, DateTime? FromUtc)
{
    /// <summary>The server's whole history is answerable (the Manager API).</summary>
    public static DealHistoryWindow Full => new(true, null);

    /// <summary>The provider holds no deals at all.</summary>
    public static DealHistoryWindow None => new(false, null);

    /// <summary>The provider's record is complete from <paramref name="fromUtc"/> on.</summary>
    public static DealHistoryWindow Since(DateTime fromUtc) => new(false, DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc));

    /// <summary>True when nothing can be compared: the provider is partial and holds no deals.</summary>
    public bool IsEmpty => !Complete && FromUtc is null;

    /// <summary>Whether a deal at this time lies inside the provider's record, so that its absence there is meaningful.</summary>
    public bool Covers(DateTime utc) => Complete || (FromUtc is { } from && utc >= from);

    public override string ToString() => Complete ? "the full history" : FromUtc is { } from ? $"deals since {from:O}" : "no deals";
}
