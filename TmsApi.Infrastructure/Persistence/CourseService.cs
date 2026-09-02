using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence;

public class CourseService : ICourseService
{
    private readonly TmsDbContext _context;
    private readonly ILogger<CourseService> _logger;

    public CourseService(TmsDbContext context, ILogger<CourseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Course?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    public async Task<Course?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<List<Course>> GetAllAsync(CancellationToken ct)
    {
        return await _context.Courses
            .Include(c => c.Enrollments)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(string code, CancellationToken ct)
    {
        return await _context.Courses.AnyAsync(c => c.Code == code, ct);
    }

    public async Task AddAsync(Course course, CancellationToken ct)
    {
        await _context.Courses.AddAsync(course, ct);
        await _context.SaveChangesAsync(ct);
    }
}