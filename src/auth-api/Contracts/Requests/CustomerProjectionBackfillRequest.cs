namespace AuthApi.Contracts.Requests;

public sealed class CustomerProjectionBackfillRequest {
    public string? TenantCode { get; set; }
    public int? BatchSize { get; set; }
    public int? MaxCustomers { get; set; }
    public int? DelayMilliseconds { get; set; }
    public bool DryRun { get; set; }
}
