using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Tests;

public sealed class ReviewTests
{
    [Fact]
    public void Review_RatingAboveFive_ThrowsWithoutDatabase()
    {
        var exception = Assert.Throws<DomainException>(() => new Review(
            101,
            "Основи C#",
            new Author("u-1", "Олена"),
            new Rating(10),
            "Текст відгуку"));

        Assert.Equal("Rating must be between 1 and 5", exception.Message);
    }

    [Fact]
    public void Review_RatingBelowOne_Throws()
        => Assert.Throws<DomainException>(() => new Rating(0));

    [Fact]
    public void Review_ValidInput_StartsAtVersionZero()
    {
        var review = new Review(
            101,
            "Основи C#",
            new Author("u-1", "Олена"),
            new Rating(5),
            "Текст відгуку");

        Assert.Equal(0, review.Version);
        Assert.Equal(101, review.CourseId);
        Assert.Equal(5, review.Rating.Value);
        Assert.NotEqual(default, review.CreatedAt);
    }

    [Fact]
    public void AddComment_EmptyText_Throws()
    {
        var review = new Review(
            101,
            "Основи C#",
            new Author("u-1", "Олена"),
            new Rating(5),
            "Текст відгуку");

        Assert.Throws<DomainException>(() => review.AddComment("Андрій", " "));
        Assert.Empty(review.Comments);
    }

    [Fact]
    public void Author_SameComponents_AreEqual()
    {
        var first = new Author("u-1", "Олена");
        var second = new Author("u-1", "Олена");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
