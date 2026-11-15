using AgroConnect.Application.DTOs;

namespace AgroConnect.Application.Pagination;

public static class PageBuilder
{
    public static int Offset(int page, int size) => (int)Math.Min((page - 1L) * size, int.MaxValue);

    public static PageResponse<T> ToPage<T>(IReadOnlyList<T> items, int page, int size, int total) =>
        new(items, new PageMeta(page, size, total, Math.Max(1, (int)Math.Ceiling(total / (double)size))));
}

