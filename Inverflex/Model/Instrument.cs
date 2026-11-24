namespace Inverflex.Model;

public sealed class Instrument
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public InstrumentType Type { get; set; }
    public string Sector { get; set; } = "";
    public decimal ReferencePrice { get; set; }
    public string Currency { get; set; } = "USD";
}
