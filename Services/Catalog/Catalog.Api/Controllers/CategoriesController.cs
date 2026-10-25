using Catalog.Bll;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(
    ICategoryService categories,
    ICourseService courses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
        => Ok(await categories.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CategoryWithCoursesDto>> GetById(long id, CancellationToken ct)
        => Ok(await categories.GetByIdAsync(id, ct));

    [HttpGet("{slug}/recent-courses")]
    public async Task<ActionResult<IReadOnlyList<CourseShortDto>>> GetRecentCourses(
        string slug, [FromQuery] DateTime? since, CancellationToken ct)
        => Ok(await courses.GetRecentlyAddedAsync(
            slug, since ?? DateTime.UtcNow.AddYears(-1), ct));
}
