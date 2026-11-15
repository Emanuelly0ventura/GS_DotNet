namespace AgroConnect.Domain.Entities;

public sealed class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
