using Ardalis.Specification;
using Catalog.Domain;

namespace Catalog.Bll.Specifications;

public sealed class CoursesByInstructorSpec : Specification<Course>
{
    public CoursesByInstructorSpec(long instructorId)
    {
        Query.Where(c => c.InstructorId == instructorId)
            .Include(c => c.Instructor)
            .Include(c => c.CourseCategories).ThenInclude(pc => pc.Category)
            .OrderBy(c => c.Title);
    }
}
