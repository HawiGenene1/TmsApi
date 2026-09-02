using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<Course?> GetByCodeAsync(string code, CancellationToken ct);
    Task<Course?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Course>> GetAllAsync(CancellationToken ct);
    Task<bool> ExistsAsync(string code, CancellationToken ct);
    Task AddAsync(Course course, CancellationToken ct);
}