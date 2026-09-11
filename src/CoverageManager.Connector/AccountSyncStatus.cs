namespace CoverageManager.Connector;

/// <summary>The last account sync cycle of <see cref="MT5ManagerConnection"/>, for the diagnostics endpoint.</summary>
public sealed record AccountSyncStatus
{
    public DateTime? LastRunUtc { get; init; }
    /// <summary>Accounts read from the provider this cycle.</summary>
    public int Read { get; init; }
    /// <summary>Accounts whose fields changed since they were last written.</summary>
    public int Changed { get; init; }
    public int Written { get; init; }
    public int Requests { get; init; }
    public string? LastError { get; init; }
    public long RunsTotal { get; init; }
    public long WrittenTotal { get; init; }
}
