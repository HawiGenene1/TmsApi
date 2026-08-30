using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate correlation ID
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        // Add header BEFORE response starts
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // Log start
        _logger.LogInformation(
            "START Request: {Method} {Path} | ID: {CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId
        );

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log completion
            _logger.LogInformation(
                "END Request: {Method} {Path} | Status: {StatusCode} | Duration: {ElapsedMs}ms | ID: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId
            );
        }
    }
}