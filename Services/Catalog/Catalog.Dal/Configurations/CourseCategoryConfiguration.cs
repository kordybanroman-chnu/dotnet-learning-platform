using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class CourseCategoryConfiguration : IEntityTypeConfiguration<CourseCategory>
{
    public void Configure(EntityTypeBuilder<CourseCategory> builder)
    {
        builder.ToTable("CourseCategory");
        builder.HasKey(pc => new { pc.CourseId, pc.CategoryId });
        builder.Property(pc => pc.AddedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(pc => pc.CategoryId);

        builder.HasOne(pc => pc.Course)
            .WithMany(c => c.CourseCategories)
            .HasForeignKey(pc => pc.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pc => pc.Category)
            .WithMany(c => c.CourseCategories)
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
