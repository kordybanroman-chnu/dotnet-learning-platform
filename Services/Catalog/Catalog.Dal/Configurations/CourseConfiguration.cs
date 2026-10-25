using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses", t => t.HasCheckConstraint("CK_Courses_Price", "[Price] > 0"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Price).HasColumnType("decimal(18,2)");
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.Title);
        builder.HasIndex(c => c.InstructorId);

        builder.HasOne(c => c.Instructor)
            .WithMany(i => i.Courses)
            .HasForeignKey(c => c.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Detail)
            .WithOne()
            .HasForeignKey<CourseDetail>(d => d.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Images)
            .WithOne()
            .HasForeignKey(i => i.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
