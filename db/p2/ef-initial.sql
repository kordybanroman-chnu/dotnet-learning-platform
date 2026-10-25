IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Slug] nvarchar(150) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [Instructors] (
        [Id] bigint NOT NULL IDENTITY,
        [FullName] nvarchar(200) NOT NULL,
        [Bio] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Instructors] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] bigint NOT NULL IDENTITY,
        [Slug] nvarchar(200) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [InstructorId] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Courses_Price] CHECK ([Price] > 0),
        CONSTRAINT [FK_Courses_Instructors_InstructorId] FOREIGN KEY ([InstructorId]) REFERENCES [Instructors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseCategory] (
        [CourseId] bigint NOT NULL,
        [CategoryId] bigint NOT NULL,
        [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_CourseCategory] PRIMARY KEY ([CourseId], [CategoryId]),
        CONSTRAINT [FK_CourseCategory_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CourseCategory_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseDetails] (
        [CourseId] bigint NOT NULL,
        [Syllabus] nvarchar(max) NOT NULL,
        [DurationHours] int NOT NULL,
        CONSTRAINT [PK_CourseDetails] PRIMARY KEY ([CourseId]),
        CONSTRAINT [CK_CourseDetails_Duration] CHECK ([DurationHours] > 0),
        CONSTRAINT [FK_CourseDetails_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseImages] (
        [Id] bigint NOT NULL IDENTITY,
        [CourseId] bigint NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_CourseImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseImages_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    EXEC(N'INSERT INTO [Categories] ([Id], [CreatedAt], [Name], [Slug])
    VALUES (CAST(1 AS bigint), ''2026-01-01T00:00:00.0000000Z'', N''Програмування'', N''programming''),
    (CAST(2 AS bigint), ''2026-01-01T00:00:00.0000000Z'', N''Бази даних'', N''databases''),
    (CAST(3 AS bigint), ''2026-01-01T00:00:00.0000000Z'', N''Веброзробка'', N''web'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Bio', N'CreatedAt', N'FullName') AND [object_id] = OBJECT_ID(N'[Instructors]'))
        SET IDENTITY_INSERT [Instructors] ON;
    EXEC(N'INSERT INTO [Instructors] ([Id], [Bio], [CreatedAt], [FullName])
    VALUES (CAST(1 AS bigint), N''C# / .NET, 10 років досвіду'', ''2026-01-01T00:00:00.0000000Z'', N''Олена Петренко''),
    (CAST(2 AS bigint), N''MongoDB / високі навантаження'', ''2026-01-01T00:00:00.0000000Z'', N''Андрій Шевченко'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Bio', N'CreatedAt', N'FullName') AND [object_id] = OBJECT_ID(N'[Instructors]'))
        SET IDENTITY_INSERT [Instructors] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseCategory_CategoryId] ON [CourseCategory] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseImages_CourseId_SortOrder] ON [CourseImages] ([CourseId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_InstructorId] ON [Courses] ([InstructorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Courses_Slug] ON [Courses] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_Title] ON [Courses] ([Title]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261025094424_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261025094424_InitialCreate', N'9.0.21');
END;

COMMIT;
GO

