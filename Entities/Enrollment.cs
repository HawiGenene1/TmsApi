namespace TmsApi.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public decimal? Grade { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    

// NEW: Academic Year (e.g., "2024-2025")
    public string? AcademicYear { get; set; }
    
    // NEW: Archive flag
    public bool IsArchived { get; set; } = false;
    
    // Navigation properties
    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;
}