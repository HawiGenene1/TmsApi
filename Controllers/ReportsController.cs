using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(TmsDbContext context) : ControllerBase
{
    // 1. How many active students have GPA >= 3.0?
    [HttpGet("active-high-gpa-count")]
    public async Task<IActionResult> GetActiveHighGpaCount()
    {
        var count = await context.Students
            .Where(s => s.IsActive && s.GPA >= 3.0m)
            .CountAsync();
        
        return Ok(new { Count = count });
    }

    // 2. Which courses have the most enrollments, sorted descending?
    [HttpGet("course-enrollment-counts")]
    public async Task<IActionResult> GetCourseEnrollmentCounts()
    {
        var list = await context.Courses
            .Select(c => new { c.Title, EnrollmentCount = c.Enrollments.Count })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();
        
        return Ok(list);
    }

    // 3. What is the average GPA per course?
    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> GetAverageGpaPerCourse()
    {
        var list = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new { Course = g.Key, AverageGPA = g.Average(e => e.Student.GPA) })
            .ToListAsync();
        
        return Ok(list);
    }

    // 4. Which students have zero enrollments?
    [HttpGet("students-with-no-enrollments")]
    public async Task<IActionResult> GetStudentsWithNoEnrollments()
    {
        var list = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();
        
        return Ok(list);
    }
}