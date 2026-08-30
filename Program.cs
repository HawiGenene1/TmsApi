using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// ADD THIS - Required for UseExceptionHandler() to work in .NET 10
builder.Services.AddProblemDetails();

// 1. Register Authentication Services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// 2. Add Authorization Services
builder.Services.AddAuthorization();

// 3. Add Controllers (if you need them later)
builder.Services.AddControllers();

var app = builder.Build();

// 4. MIDDLEWARE PIPELINE (ORDER MATTERS!)

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

// 5. ENDPOINTS

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

// Map controllers (if you have any)
app.MapControllers();

app.Run();