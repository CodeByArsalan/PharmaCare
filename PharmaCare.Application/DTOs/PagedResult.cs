namespace PharmaCare.Application.DTOs;

/// <summary>
/// Non-generic view over a paged result, so shared UI (e.g. the pager partial) can render
/// any paged list without knowing the item type.
/// </summary>
public interface IPagedResult
{
    int TotalCount { get; }
    int CurrentPage { get; }
    int PageSize { get; }
    int TotalPages { get; }
}

public class PagedResult<T> : IPagedResult
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
