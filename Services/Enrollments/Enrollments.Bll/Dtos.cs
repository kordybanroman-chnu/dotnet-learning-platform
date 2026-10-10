using System.ComponentModel.DataAnnotations;

namespace Enrollments.Bll;

public record EnrollmentItemDto(long CourseId, string CourseTitle, decimal UnitPrice, int Units);

public record EnrollmentDto(long Id, long StudentId, string Status, DateTime EnrolledAt,
    string? Note, string? PreferredSchedule, IReadOnlyList<EnrollmentItemDto> Items);

public class CreateEnrollmentItemDto
{
    [Range(1, long.MaxValue)]
    public long CourseId { get; set; }
    [Range(1, 1000)]
    public int Units { get; set; } = 1;
}

public class CreateEnrollmentDto
{
    [Range(1, long.MaxValue)]
    public long StudentId { get; set; }
    [Required, MinLength(1)]
    public List<CreateEnrollmentItemDto> Items { get; set; } = new();
    [MaxLength(1000)]
    public string? Note { get; set; }
    [MaxLength(200)]
    public string? PreferredSchedule { get; set; }
}

public record StudentDto(long Id, string Email, string FullName);

public class CreateStudentDto
{
    [Required, EmailAddress, MaxLength(320)]
    public string Email { get; set; } = "";
    [Required, MaxLength(200)]
    public string FullName { get; set; } = "";
}
