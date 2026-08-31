using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TmsApi;
using TmsApi.Data;
using TmsApi.Entities;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ADD THIS - Required for UseExceptionHandler() to work in .NET 10
builder.Services.AddProblemDetails();

// Enable validation to catch captive dependencies
builder.Host.UseDefaultServiceProvider(options => 
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// 1. Register Authentication Services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// 2. Add Authorization Services
builder.Services.AddAuthorization();

// 3. Add Controllers
builder.Services.AddControllers();

// 4. Register PaymentOptions with validation
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 5. Register Services
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// Register TmsDbContext
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .LogTo(Console.WriteLine, LogLevel.Information)
        .EnableSensitiveDataLogging());

var app = builder.Build();

// 6. MIDDLEWARE PIPELINE (ORDER MATTERS!)

// First: Logging middleware (wraps everything)
app.UseMiddleware<RequestLoggingMiddleware>();

// Second: Exception handling - Use the built-in ProblemDetails
app.UseExceptionHandler();
app.UseStatusCodePages();

// Third: HTTPS redirection
app.UseHttpsRedirection();

// Fourth: Routing (must come before auth)
app.UseRouting();

// Fifth: Authentication (who are you?)
app.UseAuthentication();

// Sixth: Authorization (are you allowed?)
app.UseAuthorization();

// 7. ENVIRONMENT-AWARE ENDPOINTS
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// 8. APPLICATION ENDPOINTS

// Protected endpoint - requires authentication
app.MapGet("/api/assessments/results", () =>
{
    return Results.Ok(new
    {
        courseCode = "CS-101",
        studentId = "S-001",
        letterGrade = "A"
    });
})
.RequireAuthorization();

// Test endpoint for Exercise 2 - worker smoke test
app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
{
    worker.ProcessBatch();
    return Results.Ok("processed");
});

// Test endpoint for Exercise 4 - structured logging test
app.MapPost("/api/enrollments/test", async (IEnrollmentService service) =>
{
    await service.EnrollAsync("S-TEST-001", "CS-101");
    await service.EnrollAsync("S-TEST-001", "CS-101");
    await service.GetByIdAsync("nonexistent");
    await service.DeleteAsync("nonexistent");
    return Results.Ok("Logs generated - check your terminal");
});


// Seed test data at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    context.Database.Migrate();

    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };
        context.Students.AddRange(students);  // AddRange accepts a list

        var courses = new List<Course>
        {
            new() { Code = "CS-101", Title = "Introduction to Computer Science", Capacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", Capacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", Capacity = 40 }
        };
        context.Courses.AddRange(courses);  // AddRange accepts a list

        // Save students and courses first so they get Ids
        context.SaveChanges();

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
        };
        context.Enrollments.AddRange(enrollments);  // AddRange accepts a list
        context.SaveChanges();
    }
}

// Test endpoint for ProblemDetails (MUST BE BEFORE MapControllers OR handled by controller)
app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

// Map controllers - THIS SHOULD BE LAST
app.MapControllers();

app.Run();