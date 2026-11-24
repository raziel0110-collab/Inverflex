namespace Inverflex.Model;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public List<Portfolio> Portfolios { get; set; } = [];
}
