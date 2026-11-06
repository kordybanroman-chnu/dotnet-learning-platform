using System.Globalization;
using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public sealed class ReviewsClient(HttpClient http)
{
    public async Task<IReadOnlyList<ReviewDto>> GetForCoursesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var pages = await Task.WhenAll(courseIds.Select(id => GetPageAsync(id, ct)));
        return pages.SelectMany(page => page.Items).ToList();
    }

    private async Task<PagedResult<ReviewDto>> GetPageAsync(long courseId, CancellationToken ct)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"/api/reviews?courseId={courseId}&page=1&pageSize=100");
        var page = await http.GetFromJsonAsync<PagedResult<ReviewDto>>(url, ct);
        return page ?? throw new HttpRequestException($"reviews-api returned an empty body for course {courseId}");
    }
}
