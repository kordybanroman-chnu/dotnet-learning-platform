using Catalog.Bll;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/courses")]
public sealed class CoursesController(ICourseService service) : ControllerBase
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
        var result = await validator.ValidateAsync(input, ct);
        if (!result.IsValid)
            return ValidationErrors(result);

        var created = await service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}/price")]
    public async Task<IActionResult> UpdatePrice(
        long id,
        UpdateCoursePriceDto input,
        [FromServices] IValidator<UpdateCoursePriceDto> validator,
        CancellationToken ct)
    {
        var result = await validator.ValidateAsync(input, ct);
        if (!result.IsValid)
            return ValidationErrors(result);

        await service.UpdatePriceAsync(id, input.Price, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    private ActionResult ValidationErrors(ValidationResult result)
        => ValidationProblem(new ValidationProblemDetails(
            result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.ErrorMessage).ToArray())));
}
