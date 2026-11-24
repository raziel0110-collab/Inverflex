using System.Text.Json.Serialization;

namespace Inverflex.Model;

public sealed class InvestmentOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int ClientId { get; set; }
    public string InstrumentId { get; set; } = "";
    public OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    [JsonIgnore]
    public long CreatedAtUtcTicks { get; set; }
    [JsonIgnore]
    public long? CompletedAtUtcTicks { get; set; }
    public int? ApprovedBy { get; set; }
    public int Version { get; set; }
}
