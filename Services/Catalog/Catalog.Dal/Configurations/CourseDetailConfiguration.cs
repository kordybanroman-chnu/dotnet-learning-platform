using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Dal.Configurations;

public sealed class CourseDetailConfiguration : IEntityTypeConfiguration<CourseDetail>
{
    public void Configure(EntityTypeBuilder<CourseDetail> builder)
    {
        builder.ToTable("CourseDetails", t =>
            t.HasCheckConstraint("CK_CourseDetails_Duration", "[DurationHours] > 0"));
        builder.HasKey(d => d.CourseId);
        builder.Property(d => d.Syllabus).IsRequired();
        builder.Property(d => d.DurationHours).IsRequired();
    }
}
