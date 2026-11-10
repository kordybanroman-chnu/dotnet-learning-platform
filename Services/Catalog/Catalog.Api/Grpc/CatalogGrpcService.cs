using Catalog.Bll;
using Catalog.Domain;
using Catalog.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Catalog.Api.Grpc;

public sealed class CatalogGrpcService(ICourseService courses) : CatalogService.CatalogServiceBase
{
    public override async Task<CourseReply> GetCourse(
        GetCourseRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            var course = await courses.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(course);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<CoursesReply> GetCoursesByIds(
        GetCoursesByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Ids must be positive"));

        var reply = new CoursesReply();
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                reply.Courses.Add(ToReply(
                    await courses.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (NotFoundException)
            {
            }
        }

        return reply;
    }

    private static CourseReply ToReply(CourseDto course) => new()
    {
        Id = course.Id,
        Slug = course.Slug,
        Title = course.Title,
        Price = (double)course.Price,
        InstructorId = course.InstructorId,
        InstructorName = course.InstructorName,
        Detail = course.Detail is null
            ? null
            : new CourseDetailReply
            {
                Syllabus = course.Detail.Syllabus,
                DurationHours = course.Detail.DurationHours
            },
        Images =
        {
            course.Images.Select(image => new CourseImageReply
            {
                Id = image.Id,
                Url = image.Url,
                SortOrder = image.SortOrder
            })
        },
        Categories = { course.Categories },
        CreatedAt = ToTimestamp(course.CreatedAt)
    };

    private static Timestamp ToTimestamp(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => Timestamp.FromDateTime(value),
        DateTimeKind.Local => Timestamp.FromDateTime(value.ToUniversalTime()),
        _ => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc))
    };
}
