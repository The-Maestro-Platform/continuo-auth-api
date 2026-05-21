using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public sealed class CustomerProjectionBackfillService {
    private const int DefaultBatchSize = 5000;
    private const int MaxBatchSize = 5000;
    private const int DefaultDelayMilliseconds = 1000;
    private const int MaxDelayMilliseconds = 10000;

    private readonly AuthDbContext _db;

    public CustomerProjectionBackfillService(AuthDbContext db) {
        _db = db;
    }

    public async Task<CustomerProjectionBackfillResponse> EnqueueRegisteredEventsAsync(
        CustomerProjectionBackfillRequest request,
        CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(request);

        var batchSize = NormalizeBatchSize(request.BatchSize);
        var delayMilliseconds = NormalizeDelayMilliseconds(request.DelayMilliseconds);
        var maxCustomers = NormalizeMaxCustomers(request.MaxCustomers);
        var tenantCode = string.IsNullOrWhiteSpace(request.TenantCode) ? null : request.TenantCode.Trim();

        var totalEligibleCustomers = await BuildCustomerQuery(tenantCode).CountAsync(ct);
        var targetCustomerCount = maxCustomers.HasValue
            ? Math.Min(totalEligibleCustomers, maxCustomers.Value)
            : totalEligibleCustomers;

        var scannedCustomers = 0;
        var enqueuedMessages = 0;
        var batchCount = 0;

        while (scannedCustomers < targetCustomerCount) {
            var take = Math.Min(batchSize, targetCustomerCount - scannedCustomers);
            var customers = await BuildCustomerQuery(tenantCode)
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Skip(scannedCustomers)
                .Take(take)
                .ToListAsync(ct);

            if (customers.Count == 0) {
                break;
            }

            if (!request.DryRun) {
                foreach (var customer in customers) {
                    _db.OutboxMessages.Add(CustomerOutboxFactory.Registered(customer, customer.Tenant.Code));
                }

                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
            }

            scannedCustomers += customers.Count;
            enqueuedMessages += request.DryRun ? 0 : customers.Count;
            batchCount += 1;

            if (scannedCustomers < targetCustomerCount && delayMilliseconds > 0) {
                await Task.Delay(delayMilliseconds, ct);
            }
        }

        return new CustomerProjectionBackfillResponse {
            TenantCode = tenantCode,
            BatchSize = batchSize,
            DelayMilliseconds = delayMilliseconds,
            TotalEligibleCustomers = totalEligibleCustomers,
            ScannedCustomers = scannedCustomers,
            EnqueuedMessages = enqueuedMessages,
            BatchCount = batchCount,
            DryRun = request.DryRun
        };
    }

    private IQueryable<Customer> BuildCustomerQuery(string? tenantCode) {
        var query = _db.Customers
            .AsNoTracking()
            .Include(x => x.Tenant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            query = query.Where(x =>
                x.Tenant.Code == tenantCode ||
                x.Tenant.Slug == tenantCode ||
                x.Tenant.Subdomain == tenantCode);
        }

        return query;
    }

    private static int NormalizeBatchSize(int? requestedBatchSize) {
        if (!requestedBatchSize.HasValue || requestedBatchSize.Value <= 0) {
            return DefaultBatchSize;
        }

        return Math.Min(requestedBatchSize.Value, MaxBatchSize);
    }

    private static int NormalizeDelayMilliseconds(int? requestedDelayMilliseconds) {
        if (!requestedDelayMilliseconds.HasValue) {
            return DefaultDelayMilliseconds;
        }

        if (requestedDelayMilliseconds.Value < 0) {
            return 0;
        }

        return Math.Min(requestedDelayMilliseconds.Value, MaxDelayMilliseconds);
    }

    private static int? NormalizeMaxCustomers(int? requestedMaxCustomers) {
        if (!requestedMaxCustomers.HasValue || requestedMaxCustomers.Value <= 0) {
            return null;
        }

        return requestedMaxCustomers.Value;
    }
}
