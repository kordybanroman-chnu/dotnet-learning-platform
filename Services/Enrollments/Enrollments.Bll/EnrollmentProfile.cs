using AutoMapper;
using Enrollments.Domain;

namespace Enrollments.Bll;

public sealed class EnrollmentProfile : Profile
{
    public EnrollmentProfile()
    {
        CreateMap<EnrollmentItem, EnrollmentItemDto>();
        CreateMap<Enrollment, EnrollmentDto>();
        CreateMap<Student, StudentDto>();
    }
}
