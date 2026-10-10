namespace Enrollments.Domain;

public sealed class Student
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
}

public sealed class Enrollment
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public string Status { get; set; } = EnrollmentStatus.Submitted;
    public DateTime EnrolledAt { get; set; }
    public string? Note { get; set; }
    public string? PreferredSchedule { get; set; }
    public List<EnrollmentItem> Items { get; } = new();
}

public sealed class EnrollmentItem
{
    public long EnrollmentId { get; set; }
    public long CourseId { get; set; }
    public string CourseTitle { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Units { get; set; }
}

public sealed class Course
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public int SeatsAvailable { get; set; }
}

public static class EnrollmentStatus
{
    public const string Submitted = "Submitted";
    public const string AwaitingValidation = "AwaitingValidation";
    public const string SeatsConfirmed = "SeatsConfirmed";
    public const string Paid = "Paid";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
