namespace TmsApi;

public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();
        var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
        
        enrollmentService.EnrollAsync("BATCH-001", "CS-101");
        enrollmentService.EnrollAsync("BATCH-002", "CS-102");
        enrollmentService.EnrollAsync("BATCH-003", "CS-103");
    }
}