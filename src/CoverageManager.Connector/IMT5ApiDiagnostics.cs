namespace CoverageManager.Connector;

/// <summary>
/// Optional, provider-specific runtime counters. <see cref="MT5ManagerConnection.ApiDiagnostics"/> surfaces them on
/// <c>/api/exposure/diagnostics.liveBridge</c> when the active <see cref="IMT5Api"/> implements this.
/// </summary>
public interface IMT5ApiDiagnostics
{
    IReadOnlyDictionary<string, object?> Diagnostics();
}
