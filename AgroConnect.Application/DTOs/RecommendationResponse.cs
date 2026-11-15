namespace AgroConnect.Application.DTOs;

public sealed record RecommendationResponse(Guid Id, DateTimeOffset GeneratedAt, string Priority, string Title, string Message, string Reason, ReadingSnapshot[] BasedOn, string Disclaimer, bool SimulatedData);
