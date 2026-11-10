using Enrollments.Bll;
using Enrollments.Domain;
using Enrollments.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Enrollments.Api.Grpc;

public sealed class EnrollmentsGrpcService(IEnrollmentService enrollments)
    : EnrollmentsService.EnrollmentsServiceBase
{
    public override async Task<EnrollmentReply> GetEnrollment(
        GetEnrollmentRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive"));

        try
        {
            return ToReply(await enrollments.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    private static EnrollmentReply ToReply(EnrollmentDto enrollment) => new()
    {
        Id = enrollment.Id,
        StudentId = enrollment.StudentId,
        Status = enrollment.Status,
        EnrolledAt = ToTimestamp(enrollment.EnrolledAt),
        Note = enrollment.Note ?? "",
        PreferredSchedule = enrollment.PreferredSchedule ?? "",
        Items =
        {
            enrollment.Items.Select(item => new EnrollmentItemReply
            {
                CourseId = item.CourseId,
                CourseTitle = item.CourseTitle,
                UnitPrice = (double)item.UnitPrice,
                Units = item.Units
            })
        }
    };

    private static Timestamp ToTimestamp(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => Timestamp.FromDateTime(value),
        DateTimeKind.Local => Timestamp.FromDateTime(value.ToUniversalTime()),
        _ => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc))
    };
}
