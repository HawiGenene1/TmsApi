using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly LinkGenerator _linkGenerator;

    public CoursesController(ICourseService courseService, LinkGenerator linkGenerator)
    {
        _courseService = courseService;
        _linkGenerator = linkGenerator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PageResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription("Returns a paginated, optionally filtered list of TMS courses. PageSize is capped at 50.")]
    public async Task<IActionResult> GetCourses([FromQuery] PageRequest request,
        CancellationToken ct)
    {
        var result = await _courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get a course by ID")]
    [EndpointDescription("Returns course details with HATEOAS links. Returns 404 if the course does not exist.")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await _courseService.GetByIdAsync(id, ct);
        if (course is null) return NotFound();

        var links = new List<LinkDto>();

        var selfLink = _linkGenerator.GetPathByName(HttpContext,
            nameof(GetCourseById),
            new { id });
        links.Add(new LinkDto(selfLink!, "self", "GET"));
        links.Add(new LinkDto(selfLink!, "update", "PUT"));
        links.Add(new LinkDto(selfLink!, "delete", "DELETE"));

        var enrollmentsLink = _linkGenerator.GetPathByName(HttpContext,
            "ListCourseEnrollments",
            new { courseId = id });
        links.Add(new LinkDto(enrollmentsLink!, "enrollments", "GET"));

        if (course.EnrollmentCount < course.MaxCapacity)
        {
            links.Add(new LinkDto(enrollmentsLink!, "enroll", "POST"));
        }

        var detailDto = new CourseDetailDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            MaxCapacity = course.MaxCapacity,
            EnrollmentCount = course.EnrollmentCount,
            Links = links
        };

        return Ok(detailDto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription("Creates a course with a unique code. Returns 409 if the course code already exists.")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (await _courseService.CodeExistsAsync(request.Code, ct))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course code already exists",
                Detail = $"A course with code '{request.Code}' is already registered.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var result = await _courseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }
}
