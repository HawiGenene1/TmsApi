using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options; // For Options validation
using TmsApi;

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

// 3. Add Controllers (if you need them later)
builder.Services.AddControllers();

// 4. Register PaymentOptions with validation
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 5. Register Services
builder.Services.AddSingleton<EnrollmentWorker>();     // Singleton
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();  // Scoped

var app = builder.Build();

// 6. MIDDLEWARE PIPELINE (ORDER MATTERS!)

// First: Logging middleware (wraps everything)
app.UseMiddleware<RequestLoggingMiddleware>();

// Second: Exception handling (catch errors early)
app.UseExceptionHandler();

// Third: HTTPS redirection
app.UseHttpsRedirection();

// Fourth: Routing (must come before auth)
app.UseRouting();

// Fifth: Authentication (who are you?)
app.UseAuthentication();

// Sixth: Authorization (are you allowed?)
app.UseAuthorization();

// 7. ENDPOINTS

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
    // Test duplicate detection
    await service.EnrollAsync("S-TEST-001", "CS-101");
    await service.EnrollAsync("S-TEST-001", "CS-101"); // This should trigger the duplicate warning
    
    // Test GetById not found
    await service.GetByIdAsync("nonexistent");
    
    // Test Delete not found
    await service.DeleteAsync("nonexistent");
    
    return Results.Ok("Logs generated - check your terminal");
});

// Map controllers (if you have any)
app.MapControllers();

app.Run();