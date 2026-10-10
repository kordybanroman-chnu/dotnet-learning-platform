IF NOT EXISTS (SELECT 1 FROM dbo.Instructors WHERE FullName = N'Олена Петренко')
    INSERT INTO dbo.Instructors (FullName, Bio)
    VALUES (N'Олена Петренко', N'C# / .NET, 10 років досвіду');
IF NOT EXISTS (SELECT 1 FROM dbo.Instructors WHERE FullName = N'Андрій Шевченко')
    INSERT INTO dbo.Instructors (FullName, Bio)
    VALUES (N'Андрій Шевченко', N'MongoDB / високі навантаження');
GO

MERGE INTO dbo.Categories AS t
USING (VALUES
    (N'Програмування', N'programming'),
    (N'Бази даних',    N'databases'),
    (N'Веброзробка',   N'web')
) AS s (Name, Slug)
ON t.Slug = s.Slug
WHEN NOT MATCHED THEN
    INSERT (Name, Slug) VALUES (s.Name, s.Slug);
GO

SET IDENTITY_INSERT dbo.Courses ON;
MERGE INTO dbo.Courses AS t
USING (VALUES
    (101, N'csharp-basics',  N'Основи C#',        CAST(1200.00 AS DECIMAL(18,2)),
        (SELECT MIN(Id) FROM dbo.Instructors)),
    (102, N'aspnet-core-api', N'ASP.NET Core API', CAST(1800.00 AS DECIMAL(18,2)),
        (SELECT MIN(Id) FROM dbo.Instructors)),
    (103, N'mongodb-dotnet', N'MongoDB для .NET', CAST(1500.00 AS DECIMAL(18,2)),
        (SELECT MAX(Id) FROM dbo.Instructors))
) AS s (Id, Slug, Title, Price, InstructorId)
ON t.Slug = s.Slug
WHEN NOT MATCHED THEN
    INSERT (Id, Slug, Title, Price, InstructorId)
    VALUES (s.Id, s.Slug, s.Title, s.Price, s.InstructorId);
SET IDENTITY_INSERT dbo.Courses OFF;
GO

INSERT INTO dbo.CourseDetails (CourseId, Syllabus, DurationHours)
SELECT c.Id, N'Програма курсу', 20 FROM dbo.Courses c
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseDetails d WHERE d.CourseId = c.Id);
GO

INSERT INTO dbo.CourseCategory (CourseId, CategoryId)
SELECT c.Id, cat.Id
FROM dbo.Courses c CROSS JOIN dbo.Categories cat
WHERE (c.Slug = 'csharp-basics' AND cat.Slug = 'programming')
   OR (c.Slug = 'aspnet-core-api' AND cat.Slug IN ('programming', 'web'))
   OR (c.Slug = 'mongodb-dotnet' AND cat.Slug = 'databases')
EXCEPT
SELECT CourseId, CategoryId FROM dbo.CourseCategory;
GO

INSERT INTO dbo.CourseImages (CourseId, Url, SortOrder)
SELECT c.Id, N'/img/' + c.Slug + '.jpg', 0 FROM dbo.Courses c
WHERE NOT EXISTS (SELECT 1 FROM dbo.CourseImages i WHERE i.CourseId = c.Id);
GO
