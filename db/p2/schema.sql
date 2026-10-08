CREATE TABLE dbo.Instructors (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    FullName  NVARCHAR(200) NOT NULL,
    Bio       NVARCHAR(2000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Categories (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    Name      NVARCHAR(150) NOT NULL,
    Slug      NVARCHAR(150) NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Courses (
    Id           BIGINT IDENTITY(1,1) PRIMARY KEY,
    Slug         NVARCHAR(200) NOT NULL UNIQUE,
    Title        NVARCHAR(200) NOT NULL,
    Price        DECIMAL(18,2) NOT NULL CHECK (Price > 0),
    InstructorId BIGINT NOT NULL REFERENCES dbo.Instructors (Id),
    CreatedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.CourseDetails (
    CourseId      BIGINT PRIMARY KEY
                  REFERENCES dbo.Courses (Id) ON DELETE CASCADE,
    Syllabus      NVARCHAR(MAX) NOT NULL,
    DurationHours INT NOT NULL CHECK (DurationHours > 0)
);

CREATE TABLE dbo.CourseCategory (
    CourseId   BIGINT NOT NULL REFERENCES dbo.Courses (Id) ON DELETE CASCADE,
    CategoryId BIGINT NOT NULL REFERENCES dbo.Categories (Id) ON DELETE CASCADE,
    AddedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (CourseId, CategoryId)
);

CREATE TABLE dbo.CourseImages (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    CourseId  BIGINT NOT NULL REFERENCES dbo.Courses (Id) ON DELETE CASCADE,
    Url       NVARCHAR(500) NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0
);
GO

CREATE INDEX IX_Courses_Title ON dbo.Courses (Title);
GO
CREATE INDEX IX_Courses_InstructorId ON dbo.Courses (InstructorId);
GO
CREATE INDEX IX_CourseCategory_CategoryId ON dbo.CourseCategory (CategoryId);
GO
CREATE INDEX IX_CourseImages_CourseId ON dbo.CourseImages (CourseId, SortOrder);
GO
