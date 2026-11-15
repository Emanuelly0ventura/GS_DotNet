namespace AgroConnect.Domain.Entities;

public sealed class PushToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Token { get; set; } = "";
    public Guid SessionId { get; set; }
    public string Platform { get; set; } = "android";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
