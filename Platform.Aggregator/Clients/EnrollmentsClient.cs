using System.Net;
using System.Net.Http.Json;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public sealed class EnrollmentsClient(HttpClient http)
{
    public async Task<EnrollmentDto?> GetEnrollmentAsync(long id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/enrollments/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EnrollmentDto>(ct);
    }
}
