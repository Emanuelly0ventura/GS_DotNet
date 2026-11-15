namespace AgroConnect.Application.DTOs;

public sealed record PageResponse<T>(IReadOnlyList<T> Items, PageMeta Page);
