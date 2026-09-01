using MediatR;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Queries;

public record GetStudentScheduleQuery(int StudentId) : IRequest<IReadOnlyList<EnrollmentResponseDto>>;

public class GetStudentScheduleHandler : IRequestHandler<GetStudentScheduleQuery, IReadOnlyList<EnrollmentResponseDto>>
{
    private readonly IEnrollmentService _enrollmentService;

    public GetStudentScheduleHandler(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> Handle(GetStudentScheduleQuery request, CancellationToken cancellationToken)
    {
        return await _enrollmentService.GetByStudentAsync(request.StudentId, cancellationToken);
    }
}
