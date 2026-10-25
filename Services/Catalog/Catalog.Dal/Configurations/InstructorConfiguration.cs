using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class InstructorConfiguration : IEntityTypeConfiguration<Instructor>
{
    public void Configure(EntityTypeBuilder<Instructor> builder)
    {
        builder.ToTable("Instructors");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.FullName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Bio).HasMaxLength(2000);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasData(
            new
            {
                Id = 1L,
                FullName = "Олена Петренко",
                Bio = "C# / .NET, 10 років досвіду",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = 2L,
                FullName = "Андрій Шевченко",
                Bio = "MongoDB / високі навантаження",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
