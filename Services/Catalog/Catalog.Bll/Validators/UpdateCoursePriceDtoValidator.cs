using FluentValidation;

namespace Catalog.Bll.Validators;

public sealed class UpdateCoursePriceDtoValidator : AbstractValidator<UpdateCoursePriceDto>
{
    public UpdateCoursePriceDtoValidator()
    {
        RuleFor(x => x.Price)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);
    }
}
