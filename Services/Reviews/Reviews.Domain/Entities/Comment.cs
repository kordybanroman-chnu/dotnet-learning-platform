using Reviews.Domain.Common;
using Reviews.Domain.Exceptions;

namespace Reviews.Domain.Entities;

public sealed class Comment : BaseEntity
{
    public string Author { get; private set; } = "";
    public string Text { get; private set; } = "";

    private Comment() { }

    public Comment(string author, string text)
        : this(author, text, DateTime.UtcNow) { }

    public Comment(string author, string text, DateTime createdAt)
        : base(createdAt)
    {
        if (string.IsNullOrWhiteSpace(author))
            throw new DomainException("Comment author is required");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Comment text is required");

        Author = author.Trim();
        Text = text.Trim();
    }
}
