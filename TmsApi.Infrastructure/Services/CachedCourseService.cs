using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Dtos;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Services;

public class CachedCourseService : ICachedCourseService
{
    private const string SchemaVersion = "v2";
    private const string CoursesTag = "courses";

    private readonly HybridCache _cache;
    private readonly ICourseService _courseService;
    private readonly ILogger<CachedCourseService> _logger;

    public CachedCourseService(
        HybridCache cache,
        ICourseService courseService,
        ILogger<CachedCourseService> logger)
    {
        _cache = cache;
        _courseService = courseService;
        _logger = logger;
    }

    public async Task<CourseDto> GetCourseAsync(string code, CancellationToken ct)
    {
        var key = $"{SchemaVersion}:course:{code}";
        var dbHit = false;

        var dto = await _cache.GetOrCreateAsync(
            key,
            async (cancellationToken) =>
            {
                dbHit = true;
                _logger.LogInformation("Cache MISS for {Key} fetching from DB", key);
                
                var course = await _courseService.GetByCodeAsync(code, ct);
                if (course is null)
                {
                    throw new KeyNotFoundException($"Course {code} not found.");
                }

                return new CourseDto(
                    course.Id,
                    course.Title,
                    course.Code,
                    course.MaxCapacity,
                    course.Enrollments.Count);
            },
            tags: [CoursesTag],
            cancellationToken: ct
        );

        if (!dbHit)
        {
            _logger.LogInformation("Cache HIT for {Key}", key);
        }

        return dto;
    }

    public async Task<List<CourseDto>> GetAllCoursesAsync(CancellationToken ct)
    {
        var key = $"{SchemaVersion}:courses:all";
        var dbHit = false;

        var list = await _cache.GetOrCreateAsync(
            key,
            async (cancellationToken) =>
            {
                dbHit = true;
                _logger.LogInformation("Cache MISS for {Key} fetching from DB", key);

                var courses = await _courseService.GetAllAsync(ct);
                return courses.Select(c => new CourseDto(
                    c.Id,
                    c.Title,
                    c.Code,
                    c.MaxCapacity,
                    c.Enrollments.Count)).ToList();
            },
            tags: [CoursesTag],
            cancellationToken: ct
        );

        if (!dbHit)
        {
            _logger.LogInformation("Cache HIT for {Key}", key);
        }

        return list;
    }

    public async Task InvalidateCourseCacheAsync(CancellationToken ct)
    {
        _logger.LogInformation("Invalidating cache tag {Tag}", CoursesTag);
        await _cache.RemoveByTagAsync(CoursesTag, ct);
    }
}