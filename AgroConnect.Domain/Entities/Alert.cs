namespace AgroConnect.Domain.Entities;

public sealed class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string Severity { get; set; } = "INFO";
    public string Status { get; set; } = "UNREAD";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool PushPending { get; set; }
    public string BasedOnJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
