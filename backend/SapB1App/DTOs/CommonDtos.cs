namespace SapB1App.DTOs;

/// <summary>Enveloppe standard pour toutes les réponses API.</summary>
public class ApiResponse<T>
{
    public bool    Success    { get; set; }
    public string? Message    { get; set; }
    public T?      Data       { get; set; }
    public int     TotalCount { get; set; }

    public ApiResponse() { }

    public ApiResponse(bool success, string? message, T? data, int totalCount = 0)
    {
        Success    = success;
        Message    = message;
        Data       = data;
        TotalCount = totalCount;
    }
}

/// <summary>Résultat paginé générique.</summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items      { get; set; } = Enumerable.Empty<T>();
    public int            TotalCount { get; set; }
    public int            Page       { get; set; }
    public int            PageSize   { get; set; }

    public PagedResult() { }

    public PagedResult(IEnumerable<T> items, int totalCount, int page, int pageSize)
    {
        Items      = items;
        TotalCount = totalCount;
        Page       = page;
        PageSize   = pageSize;
    }
}
