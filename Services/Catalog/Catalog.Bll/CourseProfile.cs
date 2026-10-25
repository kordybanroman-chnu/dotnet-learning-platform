using AutoMapper;
using Catalog.Domain;

namespace Catalog.Bll;

public sealed class CourseProfile : Profile
{
    public CourseProfile()
    {
        CreateMap<CourseDetail, CourseDetailDto>();
        CreateMap<CourseImage, CourseImageDto>();

        CreateMap<Course, CourseDto>()
            .ForCtorParam(nameof(CourseDto.InstructorName),
                o => o.MapFrom(s => s.Instructor.FullName))
            .ForCtorParam(nameof(CourseDto.Categories),
                o => o.MapFrom(s => s.CourseCategories.Select(pc => pc.Category.Name).ToList()));

        CreateMap<Course, CourseListItemDto>()
            .ForCtorParam(nameof(CourseListItemDto.InstructorName),
                o => o.MapFrom(s => s.Instructor.FullName))
            .ForCtorParam(nameof(CourseListItemDto.Categories),
                o => o.MapFrom(s => s.CourseCategories.Select(pc => pc.Category.Name).ToList()));

        CreateMap<Course, CourseShortDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<Category, CategoryWithCoursesDto>()
            .ForCtorParam(nameof(CategoryWithCoursesDto.Courses),
                o => o.MapFrom(s => s.CourseCategories.Select(pc => pc.Course).ToList()));
    }
}
