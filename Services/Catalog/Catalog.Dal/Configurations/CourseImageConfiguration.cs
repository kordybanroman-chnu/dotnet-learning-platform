using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class CourseImageConfiguration : IEntityTypeConfiguration<CourseImage>
{
    public void Configure(EntityTypeBuilder<CourseImage> builder)
    {
        builder.ToTable("CourseImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Url).IsRequired().HasMaxLength(500);
        builder.Property(i => i.SortOrder).HasDefaultValue(0);
        builder.HasIndex(i => new { i.CourseId, i.SortOrder });
    }
}
