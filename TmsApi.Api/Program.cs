using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;  
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TmsApi;
using TmsApi.Api.Handlers;
using TmsApi.Api.Hubs;
using TmsApi.Api.Middleware;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Filters;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Api.Authorization;



var builder = WebApplication.CreateBuilder(args);

// ADD THIS - Required for UseExceptionHandler() to work in .NET 10
builder.Services.AddProblemDetails();

// Add authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanEditCourse", policy =>
        policy.Requirements.Add(new CourseInstructorRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

// Add API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.GroupName == "v1";
});

builder.Services.AddOpenApi("v2", options =>
{
    options.ShouldInclude = description => description.GroupName == "v2";
});

// Enable validation to catch captive dependencies
builder.Host.UseDefaultServiceProvider(options => 
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// 1. Register TmsDbContext
builder.Services.AddDbContext<TmsDbContext>(options =>
{
    var dbContextOptions = options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
        .LogTo(Console.WriteLine, LogLevel.Information);

    if (builder.Environment.IsDevelopment())
    {
        dbContextOptions.EnableSensitiveDataLogging();
    }
});

// 2. Configure ASP.NET Core Identity
builder.Services.AddIdentity<TmsUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<TmsDbContext>()
.AddDefaultTokenProviders();

// 3. Register TokenService
builder.Services.AddScoped<ITokenService, TokenService>();

// 4. Register Authentication Services (JWT Bearer default)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "A-Very-Long-Secret-Key-For-TMS-Auth-Stored-Safely-2026";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TmsApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TmsClient";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// 5. Add Authorization Services
builder.Services.AddAuthorization();

// Add MediatR and FluentValidation
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

// Register pipeline behaviors (Logging FIRST, Validation SECOND)
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Register exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// 6. Add SignalR
builder.Services.AddSignalR();

// 7. Register PaymentOptions with validation
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 8. Register Services
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IInMemoryEnrollmentService, InMemoryEnrollmentService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

// Load allowed origins from appsettings.Development.json
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

// Register the CORS policy in the Dependency Injection container
builder.Services.AddCors(options =>
{
    options.AddPolicy("TmsClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()  // Vital for HttpOnly auth cookies
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Add HybridCache
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

// Add Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Login endpoint - 5 attempts per minute
    options.AddPolicy("LoginPolicy", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    // Default policy
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
        {
            var key = httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
            return RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 0
            });
        }
    );

    // Concurrency limiter for transcript endpoint
    options.AddConcurrencyLimiter("transcripts", opt =>
    {
        opt.PermitLimit = 5;      // Maximum 5 in-flight transcripts
        opt.QueueLimit = 20;      // Queue up to 20 more
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Token bucket for search endpoint
    options.AddTokenBucketLimiter("search", opt =>
    {
        opt.TokenLimit = 10;
        opt.TokensPerPeriod = 5;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.QueueLimit = 2;
    });

    // Custom rejection handler for ProblemDetails
    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        var lease = context.Lease;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/problem+json";

        var retryAfter = "10";
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
        {
            retryAfter = retryAfterValue.TotalSeconds.ToString("0");
        }

        var problem = new HttpValidationProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.3",
            Title = "Too Many Requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Rate limit exceeded. Please try again later.",
            Instance = httpContext.Request.Path,
            Extensions =
            {
                ["retryAfter"] = retryAfter
            }
        };

        httpContext.Response.Headers.RetryAfter = retryAfter;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    };
});

var app = builder.Build();

// 6. MIDDLEWARE PIPELINE (ORDER MATTERS!)

// Add security headers middleware after exception handling
app.UseMiddleware<SecurityHeadersMiddleware>();
// First: Logging middleware (wraps everything)
app.UseMiddleware<RequestLoggingMiddleware>();

// Second: Exception handling - Use the built-in ProblemDetails
app.UseExceptionHandler();
app.UseStatusCodePages();

// Third: HTTPS redirection
app.UseHttpsRedirection();

// Fourth: Routing (must come before auth)
app.UseRouting();

// Rate limiting (after routing, before auth)
app.UseRateLimiter();

// CORS (must be after UseRouting and before auth/endpoint mapping)
app.UseCors("TmsClient");

// Fifth: Authentication (who are you?)
app.UseAuthentication();

// Sixth: Authorization (are you allowed?)
app.UseAuthorization();

app.UseMiddleware<V1DeprecationMiddleware>();

// 7. ENVIRONMENT-AWARE ENDPOINTS
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/Scalar/V1", options =>
    {
        options.WithTitle("TMS API Reference")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .AddDocument("v1", "API Version 1.0")
            .AddDocument("v2", "API Version 2.0");
    });
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
app.MapPost("/api/enrollments/test", async (IInMemoryEnrollmentService service) =>
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

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TmsUser>>();

    foreach (var role in new[] { "Admin", "Instructor", "Student" })
    {
        if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
        {
            roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }
    }

    var instructorEmail = "leul.instructor@cotbe.edu.et";
    var instructor = userManager.FindByEmailAsync(instructorEmail).GetAwaiter().GetResult();
    if (instructor == null)
    {
        instructor = new TmsUser
        {
            UserName = instructorEmail,
            Email = instructorEmail,
            EmailConfirmed = true,
            FirstName = "Leul",
            LastName = "Gebre",
            Department = "Computer Science"
        };
        var createResult = userManager.CreateAsync(instructor, "SecurePass123!").GetAwaiter().GetResult();
        if (createResult.Succeeded)
        {
            userManager.AddToRoleAsync(instructor, "Instructor").GetAwaiter().GetResult();
        }
    }

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
            new() { Code = "CS-101", Title = "Introduction to Computer Science" },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms" },
            new() { Code = "MAT-101", Title = "Calculus I" }
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

// SignalR Hub endpoint
app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");

// Map controllers - THIS SHOULD BE LAST
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // Startup seeding is handled earlier in this file; DataSeeder is no longer used.
}

app.Run();