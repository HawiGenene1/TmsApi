using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;

namespace TmsApi.Data;

public static class DataSeeder
{
    private static readonly (string Code, string Title, int MaxCapacity)[] Courses =
    [
        ("CSE-101", "Web Development Fundamentals", 30),
        ("CSE-102", "TypeScript Essentials", 30),
        ("CSE-103", "Git and Collaborative Workflows", 25),
        ("CSE-201", "ASP.NET Core Fundamentals", 28),
        ("CSE-202", "Entity Framework Core and PostgreSQL", 28),
        ("CSE-203", "Building RESTful Web APIs", 28),
        ("CSE-301", "Advanced Web API Patterns", 24),
        ("CSE-302", "Angular Fundamentals", 26),
        ("CSE-303", "Angular Advanced", 24),
        ("CSE-304", "Full-Stack Integration", 22),
        ("CSE-305", "Testing and Quality Assurance", 22),
        ("CSE-306", "Security and Authentication", 20),
        ("CSE-401", "Cloud Deployment and DevOps", 20),
        ("CSE-402", "Microservices Architecture", 18),
        ("CSE-403", "Containerization with Docker", 22),
        ("CSE-404", "Kubernetes Orchestration", 18),
        ("CSE-405", "Monitoring and Observability", 20),
        ("CSE-406", "Performance Optimization", 18),
        ("MAT-101", "Calculus I", 40),
        ("MAT-201", "Linear Algebra", 35),
        ("MAT-301", "Probability and Statistics", 30),
        ("PHY-101", "Physics I", 35),
        ("PHY-201", "Physics II", 30),
        ("ENG-101", "Technical Writing", 25),
        ("ENG-201", "Communication Skills", 25)
    ];

    public static async Task SeedAsync(TmsDbContext context, CancellationToken ct = default)
    {
        await context.Database.MigrateAsync(ct);

        if (await context.Courses.AnyAsync(ct))
        {
            return; // Already seeded
        }

        foreach (var (code, title, maxCapacity) in Courses)
        {
            context.Courses.Add(new Course
            {
                Code = code,
                Title = title,
                MaxCapacity = maxCapacity
            });
        }

        await context.SaveChangesAsync(ct);
    }
}