using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        // Primary key
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.Grade)
            .HasPrecision(3, 2);  // e.g., 3.80

        builder.Property(e => e.EnrolledAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Foreign keys
        builder.Property(e => e.StudentId)
            .IsRequired();

        builder.Property(e => e.CourseId)
            .IsRequired();


        // NEW: Academic Year
        builder.Property(e => e.AcademicYear)
            .HasMaxLength(20);
            
        // NEW: Archive flag
        builder.Property(e => e.IsArchived)
            .IsRequired()
            .HasDefaultValue(false);

        // Foreign keys
        builder.Property(e => e.StudentId).IsRequired();
        builder.Property(e => e.CourseId).IsRequired();

        // Relationships (already configured in Student/Course configs)
        // The foreign keys are configured in the parent configs with OnDelete.Restrict
    }
}