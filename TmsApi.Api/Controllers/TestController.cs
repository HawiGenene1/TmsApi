using Microsoft.AspNetCore.Mvc;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    // Deferred Execution Experiment
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>> STEP 1: Building the query object (no database contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);

        Console.WriteLine("\n>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);

        Console.WriteLine(">> STEP 3: Materializing query into a C# List...");
        var results = orderedQuery.ToList(); // Execution is triggered here

        Console.WriteLine(">> STEP 4: Materialization finished. List populated.\n");
        return Ok(results);
    }

    // Non-translatable helper method
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    // SQL Translation Failure Experiment
    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // EF Core cannot translate this
                .ToList();
            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }

    // Fix for translation failure (inline logic)
    [HttpGet("translation-fixed")]
    public IActionResult TestTranslationFixed()
    {
        Console.WriteLine("\n>> STEP 1: Running translatable query...");
        var students = context.Students
            .Where(s => s.GPA >= 3.5m) // This can be translated to SQL
            .ToList();
        return Ok(students);
    }
}