using Microsoft.AspNetCore.Mvc;
using TmsApi;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
public class EnrollmentsController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly IEnrollmentService _enrollmentService;

    public EnrollmentsController(ICourseService courseService, IEnrollmentService enrollmentService)
    {
        _courseService = courseService;
        _enrollmentService = enrollmentService;
    }

    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await _enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> EnrollStudent(int courseId, [FromBody] EnrollStudentRequest request, CancellationToken ct)
    {
        // 1. Check if course exists
        var course = await _courseService.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return NotFound();
        }

        // 2. Check if course is full
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course is full",
                Detail = $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // 3. Create enrollment
        var result = await _enrollmentService.CreateAsync(courseId, request, ct);
        return CreatedAtAction(nameof(GetEnrollment), new { courseId, id = result.Id }, result);
    }


    // DELETE: /api/enrollments/{id} - Deletes an enrollment
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int courseId, int id, CancellationToken ct)
    {
        var deleted = await _enrollmentService.DeleteAsync(courseId, id);
        return deleted ? NoContent() : NotFound();
    }
}

// Request model
public record CreateEnrollmentRequest(string StudentId, string CourseCode);