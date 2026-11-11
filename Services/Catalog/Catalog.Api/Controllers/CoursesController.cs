using System.Diagnostics;
using Catalog.Api.Metrics;
using Catalog.Bll;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(ICourseService service, CatalogMetrics metrics) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CourseListItemDto>>> GetList(
        [FromQuery] CourseQuery query, CancellationToken ct)
        => Ok(await service.GetListAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CourseDto>> GetById(long id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpGet("by-instructor/{instructorId:long}")]
    public async Task<ActionResult<IReadOnlyList<CourseListItemDto>>> GetByInstructor(
        long instructorId, CancellationToken ct)
        => Ok(await service.GetByInstructorAsync(instructorId, ct));

    [HttpPost]
    public async Task<ActionResult<CourseDto>> Create(
        CreateCourseDto input,
        [FromServices] IValidator<CreateCourseDto> validator,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            var result = await validator.ValidateAsync(input, ct);
            if (!result.IsValid)
                return ValidationErrors(result);

            var created = await service.CreateAsync(input, ct);
            metrics.CourseCreated(input.Syllabus is not null, input.CategoryIds.Count);
            success = true;
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        finally
        {
            metrics.RecordDuration("create", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpPut("{id:long}/title")]
    public async Task<IActionResult> UpdateTitle(
        long id,
        UpdateCourseTitleDto input,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            await service.UpdateTitleAsync(id, input.Title, ct);
            success = true;
            return NoContent();
        }
        finally
        {
            metrics.RecordDuration("rename", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpPut("{id:long}/price")]
    public async Task<IActionResult> UpdatePrice(
        long id,
        UpdateCoursePriceDto input,
        [FromServices] IValidator<UpdateCoursePriceDto> validator,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            var result = await validator.ValidateAsync(input, ct);
            if (!result.IsValid)
                return ValidationErrors(result);

            await service.UpdatePriceAsync(id, input.Price, ct);
            metrics.PriceUpdated(input.Price);
            success = true;
            return NoContent();
        }
        finally
        {
            metrics.RecordDuration("update-price", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;
        try
        {
            await service.DeleteAsync(id, ct);
            metrics.CourseDeleted();
            success = true;
            return NoContent();
        }
        finally
        {
            metrics.RecordDuration("delete", success, stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private ActionResult ValidationErrors(ValidationResult result)
        => ValidationProblem(new ValidationProblemDetails(
            result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.ErrorMessage).ToArray())));
}
