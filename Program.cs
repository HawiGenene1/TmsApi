using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TmsApi;
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

// Test endpoint for ProblemDetails (MUST BE BEFORE MapControllers OR handled by controller)
app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

// Map controllers - THIS SHOULD BE LAST
app.MapControllers();

app.Run();