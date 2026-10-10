SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CreateEnrollment
    @StudentId          BIGINT,
    @Note               NVARCHAR(1000) = NULL,
    @PreferredSchedule  NVARCHAR(200)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE Id = @StudentId AND IsDeleted = 0)
        THROW 50001, 'Student not found', 1;
    INSERT INTO dbo.Enrollments (StudentId) VALUES (@StudentId);
    DECLARE @Id BIGINT = SCOPE_IDENTITY();
    IF @Note IS NOT NULL OR @PreferredSchedule IS NOT NULL
        INSERT INTO dbo.EnrollmentDetails (EnrollmentId, Note, PreferredSchedule)
        VALUES (@Id, @Note, @PreferredSchedule);
    COMMIT TRANSACTION;
    SELECT @Id AS EnrollmentId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AddEnrollmentItem
    @EnrollmentId BIGINT,
    @CourseId     BIGINT,
    @Units        INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Enrollments WITH (UPDLOCK) WHERE Id = @EnrollmentId);
    IF @Status IS NULL
        THROW 50001, 'Enrollment not found', 1;
    IF @Status NOT IN ('Submitted', 'AwaitingValidation')
        THROW 50002, 'Items can be added only to a new enrollment', 1;
    IF @Units <= 0
        THROW 50002, 'Units must be positive', 1;
    DECLARE @Title NVARCHAR(200), @Price DECIMAL(18,2);
    SELECT @Title = Title, @Price = Price FROM dbo.Courses WHERE Id = @CourseId;
    IF @Title IS NULL
        THROW 50001, 'Course not found', 1;
    INSERT INTO dbo.EnrollmentItems (EnrollmentId, CourseId, CourseTitle, UnitPrice, Units)
        VALUES (@EnrollmentId, @CourseId, @Title, @Price, @Units);
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetEnrollmentWithItems
    @EnrollmentId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT e.Id, e.StudentId, e.Status, e.EnrolledAt,
           d.Note, d.PreferredSchedule
    FROM dbo.Enrollments e
    LEFT JOIN dbo.EnrollmentDetails d ON d.EnrollmentId = e.Id
    WHERE e.Id = @EnrollmentId AND e.IsDeleted = 0;
    SELECT EnrollmentId, CourseId, CourseTitle, UnitPrice, Units
    FROM dbo.EnrollmentItems
    WHERE EnrollmentId = @EnrollmentId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ConfirmEnrollment
    @EnrollmentId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Enrollments WITH (UPDLOCK) WHERE Id = @EnrollmentId);
    IF @Status IS NULL
        THROW 50001, 'Enrollment not found', 1;
    IF @Status NOT IN ('Submitted', 'AwaitingValidation')
        THROW 50002, 'Seats can be confirmed only for a new enrollment', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.EnrollmentItems WHERE EnrollmentId = @EnrollmentId)
        THROW 50002, 'Enrollment has no items', 1;
    IF EXISTS (
        SELECT 1
        FROM dbo.EnrollmentItems i JOIN dbo.Courses c WITH (UPDLOCK) ON c.Id = i.CourseId
        WHERE i.EnrollmentId = @EnrollmentId AND c.SeatsAvailable < i.Units
    )
        THROW 50003, 'Not enough seats', 1;
    UPDATE c SET SeatsAvailable = c.SeatsAvailable - i.Units, UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Courses c JOIN dbo.EnrollmentItems i ON i.CourseId = c.Id
    WHERE i.EnrollmentId = @EnrollmentId;
    UPDATE dbo.Enrollments
    SET Status = 'SeatsConfirmed', UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @EnrollmentId;
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_CancelEnrollment
    @EnrollmentId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Enrollments WITH (UPDLOCK) WHERE Id = @EnrollmentId);
    IF @Status IS NULL
        THROW 50001, 'Enrollment not found', 1;
    IF @Status IN ('Paid', 'Completed', 'Cancelled')
        THROW 50002, 'Only an unpaid enrollment can be cancelled', 1;
    IF @Status = 'SeatsConfirmed'
    BEGIN
        UPDATE c SET SeatsAvailable = c.SeatsAvailable + i.Units, UpdatedAt = SYSUTCDATETIME()
        FROM dbo.Courses c JOIN dbo.EnrollmentItems i ON i.CourseId = c.Id
        WHERE i.EnrollmentId = @EnrollmentId;
    END
    UPDATE dbo.Enrollments
    SET Status = 'Cancelled', UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @EnrollmentId;
    COMMIT TRANSACTION;
END;
GO
