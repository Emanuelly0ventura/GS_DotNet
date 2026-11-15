namespace AgroConnect.Application.DTOs;

public sealed record LatestReadingsResponse(Guid DeviceId, DateTimeOffset? RecordedAt, IReadOnlyList<ReadingResponse> Readings, bool SimulatedData);
