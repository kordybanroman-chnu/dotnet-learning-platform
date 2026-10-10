SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

MERGE INTO dbo.Students AS t
USING (VALUES
    (N'student1@example.com',  N'Олена Петренко'),
    (N'student2@example.com',  N'Андрій Шевченко'),
    (N'obrien@example.com',    N'O''Brien Тест')
) AS s (Email, FullName)
ON t.Email = s.Email
WHEN NOT MATCHED THEN
    INSERT (Email, FullName) VALUES (s.Email, s.FullName);
GO

MERGE INTO dbo.Courses AS t
USING (VALUES
    (101, N'Основи C#',        CAST(1200.00 AS DECIMAL(18,2)), 30),
    (102, N'ASP.NET Core API', CAST(1800.00 AS DECIMAL(18,2)), 20),
    (103, N'MongoDB для .NET', CAST(1500.00 AS DECIMAL(18,2)), 0)
) AS s (Id, Title, Price, SeatsAvailable)
ON t.Id = s.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Title, Price, SeatsAvailable) VALUES (s.Id, s.Title, s.Price, s.SeatsAvailable);
GO

DECLARE @Sid BIGINT = (SELECT Id FROM dbo.Students WHERE Email = N'student1@example.com');
IF @Sid IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Enrollments WHERE StudentId = @Sid AND IsDeleted = 0)
BEGIN
    INSERT INTO dbo.Enrollments (StudentId, Status) VALUES (@Sid, 'Submitted');
    DECLARE @Id BIGINT = SCOPE_IDENTITY();
    INSERT INTO dbo.EnrollmentDetails (EnrollmentId, Note, PreferredSchedule)
        VALUES (@Id, N'Вечірня група', N'Пн/Ср 19:00');
    INSERT INTO dbo.EnrollmentItems (EnrollmentId, CourseId, CourseTitle, UnitPrice, Units)
        SELECT @Id, 101, Title, Price, 1 FROM dbo.Courses WHERE Id = 101;
END;
GO
