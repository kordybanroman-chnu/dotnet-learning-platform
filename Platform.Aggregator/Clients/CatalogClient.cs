using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public sealed class CatalogClient(HttpClient http)
{
    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        IReadOnlyCollection<long> courseIds, CancellationToken ct)
    {
        var courses = await Task.WhenAll(courseIds.Select(id => GetCourseAsync(id, ct)));
        return courses;
    }

    private async Task<CourseDto> GetCourseAsync(long id, CancellationToken ct)
    {
        var course = await http.GetFromJsonAsync<CourseDto>($"/api/courses/{id}", ct);
        return course ?? throw new HttpRequestException($"catalog-api returned an empty body for course {id}");
    }
}
