using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Common;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]

[ApiVersion("2.0")]
public class EnrollmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EnrollmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Enroll(
        [FromBody] EnrollStudentCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created => CreatedAtAction(
                nameof(GetSchedule),
                new { studentId = created.StudentId },
                created),
            onFailure: error => error.Code switch
            {
                "course_not_found" => Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Course not found",
                    detail: error.Message,
                    type: "https://tms.local/errors/course_not_found"),

                "course_full" => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Course is full",
                    detail: error.Message,
                    type: "https://tms.local/errors/course_full"),

                "already_enrolled" => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Already enrolled",
                    detail: error.Message,
                    type: "https://tms.local/errors/already_enrolled"),

                _ => Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Enrollment failed",
                    detail: error.Message,
                    type: "https://tms.local/errors/enrollment_failed")
            });
    }

    [HttpGet("{studentId}/schedule")]
    public async Task<IActionResult> GetSchedule(int studentId, CancellationToken ct)
    {
        // TODO: Implement GetStudentScheduleQuery
        return Ok(new { studentId, message = "Schedule will be implemented in Exercise 2" });
    }
}
