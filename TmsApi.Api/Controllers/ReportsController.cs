using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(TmsDbContext context) : ControllerBase
{
    // 1. Paged list of students (page size 20, stable sort by name)
    [HttpGet("students-paged")]
    public async Task<IActionResult> GetStudentsPaged(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Always OrderBy before Skip/Take for stable pagination
        var students = await context.Students
            .OrderBy(s => s.Name)  // Stable sort
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.RegistrationNumber,
                s.Name,
                s.GPA,
                s.IsActive
            })
            .ToListAsync(cancellationToken);

        var totalCount = await context.Students.CountAsync(cancellationToken);

        return Ok(new
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Data = students
        });
    }

    // 2. Top 5 courses by enrollment count
    [HttpGet("top-courses")]
    public async Task<IActionResult> GetTopCourses(CancellationToken cancellationToken = default)
    {
        var topCourses = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(5)
            .ToListAsync(cancellationToken);

        return Ok(topCourses);
    }
}