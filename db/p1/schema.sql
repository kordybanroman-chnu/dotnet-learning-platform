-- EnrollmentsDb: PK = BIGINT IDENTITY. Id генерує лише ця БД (послідовна
-- вставка з одного джерела), тому GUID не потрібен.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE TABLE dbo.Students (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    Email      NVARCHAR(320) NOT NULL UNIQUE,
    FullName   NVARCHAR(200) NOT NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION NOT NULL
);

CREATE TABLE dbo.Enrollments (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    StudentId  BIGINT        NOT NULL REFERENCES dbo.Students (Id),
    Status     NVARCHAR(30)  NOT NULL DEFAULT 'Submitted'
        CHECK (Status IN ('Submitted', 'AwaitingValidation', 'SeatsConfirmed',
                          'Paid', 'Completed', 'Cancelled')),
    EnrolledAt DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION NOT NULL
);

CREATE TABLE dbo.EnrollmentDetails (
    EnrollmentId      BIGINT PRIMARY KEY
                      REFERENCES dbo.Enrollments (Id) ON DELETE CASCADE,
    Note              NVARCHAR(1000) NULL,
    PreferredSchedule NVARCHAR(200)  NULL
);

CREATE TABLE dbo.Courses (
    Id             BIGINT PRIMARY KEY,
    Title          NVARCHAR(200)   NOT NULL,
    Price          DECIMAL(18,2)   NOT NULL CHECK (Price > 0),
    SeatsAvailable INT             NOT NULL DEFAULT 0 CHECK (SeatsAvailable >= 0),
    UpdatedAt      DATETIME2       NULL,
    RowVersion     ROWVERSION NOT NULL
);

CREATE TABLE dbo.EnrollmentItems (
    EnrollmentId BIGINT        NOT NULL REFERENCES dbo.Enrollments (Id) ON DELETE CASCADE,
    CourseId     BIGINT        NOT NULL REFERENCES dbo.Courses (Id),
    CourseTitle  NVARCHAR(200) NOT NULL,
    UnitPrice    DECIMAL(18,2) NOT NULL CHECK (UnitPrice > 0),
    Units        INT           NOT NULL CHECK (Units > 0),
    PRIMARY KEY (EnrollmentId, CourseId)
);
GO

-- Індекс під запит "записи студента з певним статусом" (сторінка "Мої курси").
CREATE INDEX IX_Enrollments_StudentId_Status ON dbo.Enrollments (StudentId, Status);
GO
-- Індекс під запит "лише живі записи" (WHERE IsDeleted = 0).
CREATE INDEX IX_Enrollments_Active ON dbo.Enrollments (StudentId) WHERE IsDeleted = 0;
GO
-- Індекс під запит "хто записаний на курс" (перевірка попиту / розсилка).
CREATE INDEX IX_EnrollmentItems_CourseId ON dbo.EnrollmentItems (CourseId);
GO
-- Індекс під запит "живі студенти за email" (логін).
CREATE INDEX IX_Students_Email_Active ON dbo.Students (Email) WHERE IsDeleted = 0;
GO
