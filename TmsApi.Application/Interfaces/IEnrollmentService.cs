using TmsApi.Application.Dtos;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int courseId, int id, CancellationToken ct);
}