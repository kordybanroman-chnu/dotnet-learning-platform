namespace Catalog.Domain;

public sealed class Instructor
{
    public long Id { get; private set; }
    public string FullName { get; private set; } = "";
    public string? Bio { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public List<Course> Courses { get; } = new();

    private Instructor() { }

    public Instructor(string fullName, string? bio = null)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Instructor name is required");
        FullName = fullName;
        Bio = bio;
        CreatedAt = DateTime.UtcNow;
    }
}

public sealed class Category
{
    public long Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public List<CourseCategory> CourseCategories { get; } = new();

    private Category() { }

    public Category(string name, string slug)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required");
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Category slug is required");
        Name = name;
        Slug = slug;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Category name is required");
        Name = name;
    }
}

public sealed class Course
{
    public long Id { get; private set; }
    public string Slug { get; private set; } = "";
    public string Title { get; private set; } = "";
    public decimal Price { get; private set; }
    public long InstructorId { get; private set; }
    public Instructor Instructor { get; private set; } = null!;
    public CourseDetail? Detail { get; private set; }
    public List<CourseImage> Images { get; } = new();
    public List<CourseCategory> CourseCategories { get; } = new();
    public DateTime CreatedAt { get; private set; }

    private Course() { }

    public Course(string slug, string title, decimal price, long instructorId)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Course slug is required");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Course title is required");
        if (instructorId <= 0)
            throw new DomainException("Course must reference an instructor");
        SetPrice(price);
        Slug = slug;
        Title = title;
        InstructorId = instructorId;
        CreatedAt = DateTime.UtcNow;
    }

    public void ChangeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Course title is required");
        Title = title;
    }

    public void ChangePrice(decimal price) => SetPrice(price);

    public void SetDetail(string syllabus, int durationHours)
        => Detail = new CourseDetail(syllabus, durationHours);

    public CourseImage AddImage(string url, int sortOrder)
    {
        var image = new CourseImage(url, sortOrder);
        Images.Add(image);
        return image;
    }

    public void AttachToCategory(long categoryId, DateTime? addedAt = null)
    {
        if (CourseCategories.Any(pc => pc.CategoryId == categoryId))
            return;
        CourseCategories.Add(new CourseCategory(categoryId, addedAt ?? DateTime.UtcNow));
    }

    public void DetachFromCategory(long categoryId)
    {
        var link = CourseCategories.FirstOrDefault(pc => pc.CategoryId == categoryId);
        if (link is not null)
            CourseCategories.Remove(link);
    }

    private void SetPrice(decimal price)
    {
        if (price <= 0)
            throw new DomainException("Price must be positive");
        if (price > 1_000_000m)
            throw new DomainException("Price must not exceed 1000000");
        Price = price;
    }
}

public sealed class CourseDetail
{
    public long CourseId { get; private set; }
    public string Syllabus { get; private set; } = "";
    public int DurationHours { get; private set; }

    private CourseDetail() { }

    public CourseDetail(string syllabus, int durationHours)
    {
        if (string.IsNullOrWhiteSpace(syllabus))
            throw new DomainException("Syllabus is required");
        if (durationHours <= 0)
            throw new DomainException("Duration must be positive");
        Syllabus = syllabus;
        DurationHours = durationHours;
    }

    public void Update(string syllabus, int durationHours)
    {
        if (string.IsNullOrWhiteSpace(syllabus))
            throw new DomainException("Syllabus is required");
        if (durationHours <= 0)
            throw new DomainException("Duration must be positive");
        Syllabus = syllabus;
        DurationHours = durationHours;
    }
}

public sealed class CourseImage
{
    public long Id { get; private set; }
    public long CourseId { get; private set; }
    public string Url { get; private set; } = "";
    public int SortOrder { get; private set; }

    private CourseImage() { }

    public CourseImage(string url, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Image url is required");
        Url = url;
        SortOrder = sortOrder;
    }
}

public sealed class CourseCategory
{
    public long CourseId { get; private set; }
    public Course Course { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public DateTime AddedAt { get; private set; }

    private CourseCategory() { }

    public CourseCategory(long categoryId, DateTime addedAt)
    {
        if (categoryId <= 0)
            throw new DomainException("Category is required");
        CategoryId = categoryId;
        AddedAt = addedAt;
    }
}
