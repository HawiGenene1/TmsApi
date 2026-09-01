using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/performance")]
public class PerformanceController(TmsDbContext context) : ControllerBase
{
    // Part A: INTENTIONAL N+1 (for learning)
    [HttpGet("n-plus-one")]
    public async Task<IActionResult> TestNPlusOne(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n=== N+1 QUERY DEMONSTRATION ===\n");
        Console.WriteLine(">> Step 1: Loading all students (1 query)...");
        
        var students = await context.Students
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        Console.WriteLine($">> Found {students.Count} students. Loading enrollment counts...");
        
        var results = new List<object>();
        
        // This loop causes N+1 queries!
        foreach (var student in students)
        {
            Console.WriteLine($">> Querying enrollments for student {student.Id}...");
            var count = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == student.Id, cancellationToken);
            
            results.Add(new
            {
                student.Name,
                student.RegistrationNumber,
                EnrollmentCount = count
            });
        }
        
        Console.WriteLine(">> Done! Check the SQL logs to see 1 + N queries.\n");
        return Ok(results);
    }
    
    // Part B: FIXED - Shaped Query (Single round-trip)
    [HttpGet("shaped")]
    public async Task<IActionResult> TestShaped(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n=== SHAPED QUERY (FIXED) ===\n");
        Console.WriteLine(">> Single query with projection...");
        
        // Fix: Single query with projection
        var report = await context.Students
            .AsNoTracking()
            .Select(s => new
            {
                s.Name,
                s.RegistrationNumber,
                EnrollmentCount = s.Enrollments.Count
            })
            .ToListAsync(cancellationToken);
        
        Console.WriteLine($">> Found {report.Count} students with enrollment counts.");
        Console.WriteLine(">> Check the SQL logs - should be only 1 query!\n");
        
        return Ok(report);
    }
    
    // Alternative: Using Include (loads full enrollment objects)
    [HttpGet("include")]
    public async Task<IActionResult> TestInclude(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n=== INCLUDE APPROACH ===\n");
        Console.WriteLine(">> Loading students with their enrollments...");
        
        var students = await context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments)
            .ToListAsync(cancellationToken);
        
        var results = students.Select(s => new
        {
            s.Name,
            s.RegistrationNumber,
            EnrollmentCount = s.Enrollments.Count
        });
        
        Console.WriteLine(">> Check the SQL logs - should be 1 query with a JOIN!\n");
        
        return Ok(results);
    }
}