using Ardalis.Specification;
using Catalog.Domain;

namespace Catalog.Bll.Specifications;

public sealed class CourseFilterSpec : Specification<Course>
{
    public CourseFilterSpec(CourseQuery query, int page, int pageSize, bool paged = true)
    {
        Query.Where(c => true)
            .Include(c => c.Instructor)
            .Include(c => c.CourseCategories).ThenInclude(pc => pc.Category);

        if (!string.IsNullOrWhiteSpace(query.Search))
            Query.Where(c => c.Title.Contains(query.Search));

        if (!string.IsNullOrWhiteSpace(query.Category))
            Query.Where(c => c.CourseCategories.Any(pc => pc.Category.Slug == query.Category));

        if (query.InstructorId is not null)
            Query.Where(c => c.InstructorId == query.InstructorId);

        if (query.MinPrice is not null)
            Query.Where(c => c.Price >= query.MinPrice);

        if (query.MaxPrice is not null)
            Query.Where(c => c.Price <= query.MaxPrice);

        var descending = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        switch (query.SortBy.ToLowerInvariant())
        {
            case "price":
                if (descending) Query.OrderByDescending(c => c.Price);
                else Query.OrderBy(c => c.Price);
                break;
            case "createdat":
                if (descending) Query.OrderByDescending(c => c.CreatedAt);
                else Query.OrderBy(c => c.CreatedAt);
                break;
            case "title":
                if (descending) Query.OrderByDescending(c => c.Title);
                else Query.OrderBy(c => c.Title);
                break;
            default:
                Query.OrderBy(c => c.Title);
                break;
        }

        if (paged)
            Query.Skip((page - 1) * pageSize).Take(pageSize);
    }
}
