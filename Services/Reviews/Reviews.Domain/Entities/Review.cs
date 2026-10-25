using MongoDB.Bson;
using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;
using Reviews.Domain.ValueObjects;

namespace Reviews.Domain.Entities;

public sealed class Review : BaseEntity
{
    private List<Comment> _comments = new();
    private List<string> _photos = new();

    public int CourseId { get; private set; }
    public string CourseTitle { get; private set; } = "";
    public Author Author { get; private set; } = null!;
    public Rating Rating { get; private set; } = null!;
    public string Text { get; private set; } = "";
    public ObjectId? DiscussionId { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<string> Photos => _photos.AsReadOnly();

    private Review() { }

    public Review(
        int courseId,
        string courseTitle,
        Author author,
        Rating rating,
        string text,
        ObjectId? discussionId = null)
    {
        if (courseId <= 0)
            throw new DomainException("CourseId must be positive");
        if (string.IsNullOrWhiteSpace(courseTitle))
            throw new DomainException("CourseTitle is required");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Review text is required");

        CourseId = courseId;
        CourseTitle = courseTitle.Trim();
        Author = author ?? throw new DomainException("Author is required");
        Rating = rating ?? throw new DomainException("Rating is required");
        Text = text.Trim();
        DiscussionId = discussionId;
    }

    public void Update(Rating rating, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Review text is required");

        Rating = rating ?? throw new DomainException("Rating is required");
        Text = text.Trim();
        Touch();
    }

    public void AddComment(string author, string text)
    {
        _comments.Add(new Comment(author, text));
        Touch();
    }

    public void AddPhoto(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Photo url is required");
        _photos.Add(url.Trim());
        Touch();
    }
}
