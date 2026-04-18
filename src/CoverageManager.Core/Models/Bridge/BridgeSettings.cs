using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models.Bridge;

/// <summary>
/// Persisted Centroid Bridge connection settings. Singleton row in Supabase.
/// Backs the REST + WebSocket integration (CS 360 REST API v1.7).
/// </summary>
public class BridgeSettings
{
    [JsonPropertyName("id")] public Guid? Id { get; set; }

    /// <summary>
    /// Master kill-switch. When false, the backend runs Stub regardless of Mode.
    /// </summary>
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }

    /// <summary>
    /// "Stub" | "Live". Only honored when Enabled=true.
    /// </summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "Stub";

    /// <summary>
    /// Base URL. Default: https://bridge.centroidsol.com
    /// </summary>
    [JsonPropertyName("base_url")] public string BaseUrl { get; set; } = "https://bridge.centroidsol.com";

    /// <summary>
    /// Centroid broker identifier (sent on every request as x-forward-client header).
    /// Provided by Centroid Support when the API user is created.
    /// </summary>
    [JsonPropertyName("client_code")] public string ClientCode { get; set; } = string.Empty;

    /// <summary>
    /// Centroid REST user name (also sent as x-forward-user header).
    /// </summary>
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;

    /// <summary>
    /// REST password. Stored alongside existing MT5 passwords in Supabase.
    /// The UI write-only-masks this.
    /// </summary>
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;

    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True when all fields required to attempt REST login are filled.</summary>
    public bool IsLoginReady() =>
        Enabled &&
        string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(ClientCode) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password);
}
