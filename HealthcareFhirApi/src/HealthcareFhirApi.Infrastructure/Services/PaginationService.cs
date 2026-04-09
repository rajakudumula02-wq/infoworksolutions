namespace HealthcareFhirApi.Infrastructure.Services;

public class PaginationService : IPaginationService
{
    private const int DefaultCount = 20;
    private const int MaxCount = 100;

    public Bundle BuildSearchBundle(
        IEnumerable<Resource> resources,
        SearchParameters parameters,
        int totalCount,
        string baseUrl)
    {
        var items = resources.ToList();
        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = totalCount,
            Entry = items.Select(r => new Bundle.EntryComponent { Resource = r }).ToList()
        };

        bundle.Link.Add(new Bundle.LinkComponent { Relation = "self", Url = BuildUrl(baseUrl, parameters) });

        if (parameters.Skip + parameters.Take < totalCount)
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = BuildNextUrl(baseUrl, parameters) });

        if (parameters.Skip > 0)
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "previous", Url = BuildPrevUrl(baseUrl, parameters) });

        return bundle;
    }

    public (int skip, int take) ResolvePage(int? count, string? pageToken)
    {
        var take = Math.Clamp(count ?? DefaultCount, 1, MaxCount);
        var skip = pageToken is not null ? DecodePageToken(pageToken) : 0;
        return (skip, take);
    }

    private static string BuildUrl(string baseUrl, SearchParameters p)
    {
        var query = BuildQueryString(p.Filters, p.Take, p.Skip);
        return $"{baseUrl}?{query}";
    }

    private static string BuildNextUrl(string baseUrl, SearchParameters p)
    {
        var nextSkip = p.Skip + p.Take;
        var token = EncodePageToken(nextSkip);
        var query = BuildQueryString(p.Filters, p.Take, pageToken: token);
        return $"{baseUrl}?{query}";
    }

    private static string BuildPrevUrl(string baseUrl, SearchParameters p)
    {
        var prevSkip = Math.Max(0, p.Skip - p.Take);
        var query = prevSkip == 0
            ? BuildQueryString(p.Filters, p.Take, 0)
            : BuildQueryString(p.Filters, p.Take, pageToken: EncodePageToken(prevSkip));
        return $"{baseUrl}?{query}";
    }

    private static string BuildQueryString(
        Dictionary<string, string?> filters,
        int take,
        int skip = 0,
        string? pageToken = null)
    {
        var parts = new List<string> { $"_count={take}" };

        foreach (var (key, value) in filters)
            if (value is not null)
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");

        if (pageToken is not null)
            parts.Add($"_page={Uri.EscapeDataString(pageToken)}");

        return string.Join("&", parts);
    }

    private static string EncodePageToken(int offset)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(offset.ToString());
        return Convert.ToBase64String(bytes);
    }

    private static int DecodePageToken(string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            return int.TryParse(str, out var offset) ? Math.Max(0, offset) : 0;
        }
        catch
        {
            return 0;
        }
    }
}
