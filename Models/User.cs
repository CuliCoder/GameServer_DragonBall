namespace DragonSiege.Models;

public class User
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? Class { get; set; } // ENUM
    public string? Role { get; set; } // ENUM
    public bool IsBanned { get; set; }
    public DateTime? CreatedAt { get; set; }
}