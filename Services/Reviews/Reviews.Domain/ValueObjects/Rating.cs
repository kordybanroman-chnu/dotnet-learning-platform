using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.ValueObjects;

public sealed class Rating : ValueObject
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5");
        Value = value;
    }

    public static implicit operator int(Rating rating) => rating.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
