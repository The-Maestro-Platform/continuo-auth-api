namespace AuthApi.Contracts.Responses;

public sealed class CustomerProjectionBackfillResponse {
    public string? TenantCode { get; set; }
    public int BatchSize { get; set; }
    public int DelayMilliseconds { get; set; }
    public int TotalEligibleCustomers { get; set; }
    public int ScannedCustomers { get; set; }
    public int EnqueuedMessages { get; set; }
    public int BatchCount { get; set; }
    public bool DryRun { get; set; }
}
