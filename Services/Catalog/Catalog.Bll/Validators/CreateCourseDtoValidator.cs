using FluentValidation;

namespace Catalog.Bll.Validators;

public sealed class CreateCourseDtoValidator : AbstractValidator<CreateCourseDto>
{
    public CreateCourseDtoValidator()
    {
        RuleFor(x => x.Slug)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug must contain lowercase letters, digits and single dashes");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.DurationHours)
            .NotNull()
            .GreaterThan(0)
            .When(x => !string.IsNullOrWhiteSpace(x.Syllabus));

        RuleFor(x => x.CategoryIds)
            .Must(ids => ids.All(id => id > 0))
            .WithMessage("Category identifiers must be positive");
    }
}
