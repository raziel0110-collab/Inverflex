namespace Inverflex.Model;

public sealed class Portfolio
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public List<Position> Positions { get; set; } = [];
}
