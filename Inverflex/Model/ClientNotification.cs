using System.Text.Json.Serialization;

namespace Inverflex.Model;

public sealed class ClientNotification
{
    public long Id { get; set; }
    public int ClientId { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    [JsonIgnore]
    public long CreatedAtUtcTicks { get; set; }
}
