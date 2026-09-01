using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public class EnrollmentService : IEnrollmentService
{
    private readonly TmsDbContext _context;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt))
            .FirstOrDefaultAsync(ct);
    }

    // NEW: Get all enrollments for a course
    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.CourseId,
                e.StudentId,
                e.EnrolledAt))
            .ToListAsync(ct);
    }

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Enrolled student {StudentId} in course {CourseId}", 
            request.StudentId, courseId);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }

    public async Task<bool> DeleteAsync(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == id && e.CourseId == courseId, ct);

        if (enrollment is null)
        {
            return false;
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted enrollment {EnrollmentId} from course {CourseId}", id, courseId);
        return true;
    }
}