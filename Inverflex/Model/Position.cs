namespace Inverflex.Model;

public sealed class Position
{
    public int Id { get; set; }
    public int PortfolioId { get; set; }
    public string InstrumentId { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
}
