using AuthApi.Contracts.Responses;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class CustomersService {
    private const int MaxConcurrencyRetries = 3;
    private readonly AuthDbContext _db;

    public CustomersService(AuthDbContext db) {
        _db = db;
    }

    public async Task<(Ulid Id, string Login)> RegisterAsync(
        string email,
        string password,
        string? displayName,
        string? phoneNumber,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? country,
        string? postalCode,
        string? tenantCode,
        bool marketingOptIn,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) {
            throw new ArgumentException("Email and password are required");
        }

        var normalizedLogin = email.Trim().ToLowerInvariant();

        var tc = tenantCode ?? "t-001";
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Code == tc || t.Slug == tc, ct);
        if (tenant == null) {
            throw new ArgumentException("Tenant code is not valid");
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (normalizedPhone != null &&
            await _db.Customers.AnyAsync(c => c.PhoneNumber == normalizedPhone && c.TenantId == tenant.Id, ct)) {
            throw new InvalidOperationException("Phone number already exists for this tenant");
        }

        // Ayni mailde aktif credential olabilir (PlatformUser/TenantUser olarak).
        // Bu durumda yeni credential acma; mevcut credential'a Customer rolünü iliştir.
        // Parolaya dokunma — kullanici eski parolasiyla girer; signup form'undaki
        // parola yok sayilir. Tek hesap, tek parola.
        var existingActiveCredential = await _db.Credentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Login == normalizedLogin && c.IsActive, ct);

        var customer = new Customer {
            Tenant = tenant,
            TenantId = tenant.Id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedLogin : displayName.Trim(),
            Email = normalizedLogin,
            PhoneNumber = normalizedPhone,
            AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim(),
            City = city?.Trim(),
            Country = country?.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            MarketingOptIn = marketingOptIn
        };
        _db.Customers.Add(customer);

        Credential credential;
        if (existingActiveCredential != null) {
            if (existingActiveCredential.CustomerId.HasValue) {
                // Bu credential zaten bir Customer'a bagli — gercek "already exists" durumu.
                throw new InvalidOperationException("Account already exists");
            }

            existingActiveCredential.Customer = customer;
            // OwnerType ilk role gore set edilmis (PlatformUser/TenantUser) — primary
            // olarak korunur; FK kolonlarinin durumu credential'in coklu rol tasidigini
            // gosterir. Parola/MustChangePassword/agreements korunur.
            credential = existingActiveCredential;
        }
        else {
            credential = new Credential {
                Login = normalizedLogin,
                Email = normalizedLogin,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                OwnerType = CredentialOwnerType.Customer,
                Customer = customer,
                IsActive = true
            };
            customer.Credentials.Add(credential);
            _db.Credentials.Add(credential);
        }

        _db.OutboxMessages.Add(CustomerOutboxFactory.Registered(customer, tenant.Code));

        await _db.SaveChangesAsync(ct);
        return (credential.Id, credential.Login);
    }

    public async Task<Credential?> GetCustomerCredentialAsync(string normalizedLogin, CancellationToken ct) {
        // Support both pure Customer credentials and cross-linked credentials
        // (e.g. PlatformUser/TenantUser with a linked Customer record via CustomerId)
        var credential = await _db.Credentials
            .Include(c => c.Customer)
                .ThenInclude(cu => cu!.Tenant)
            .FirstOrDefaultAsync(c => c.Login == normalizedLogin && c.CustomerId != null, ct);
        return credential;
    }

    public async Task<Credential?> UpdateProfileAsync(string normalizedLogin, UpdateProfileData data, CancellationToken ct) {
        return await ExecuteCustomerWriteWithRetryAsync(
            () => UpdateProfileCoreAsync(normalizedLogin, data, ct));
    }

    private async Task<Credential?> UpdateProfileCoreAsync(string normalizedLogin, UpdateProfileData data, CancellationToken ct) {
        var credential = await GetCustomerCredentialAsync(normalizedLogin, ct);
        if (credential == null || credential.Customer == null) {
            return null;
        }

        var customer = credential.Customer;
        var profileChanged = false;

        profileChanged |= SetIfProvided(data.DisplayName, value => customer.DisplayName = value, customer.DisplayName);
        profileChanged |= SetIfProvided(data.FullName, value => customer.FullName = value, customer.FullName);
        profileChanged |= SetIfProvided(data.PhoneNumber, value => customer.PhoneNumber = value, customer.PhoneNumber);
        if (data.AddressLine1 != null) {
            profileChanged |= SetNullable(value => customer.AddressLine1 = value, NormalizeNullable(data.AddressLine1), customer.AddressLine1);
        }
        if (data.AddressLine2 != null) {
            profileChanged |= SetNullable(value => customer.AddressLine2 = value, NormalizeNullable(data.AddressLine2), customer.AddressLine2);
        }
        profileChanged |= SetIfProvided(data.City, value => customer.City = value, customer.City);
        profileChanged |= SetIfProvided(data.Country, value => customer.Country = value, customer.Country);
        if (data.PostalCode != null) {
            profileChanged |= SetNullable(value => customer.PostalCode = value, NormalizeNullable(data.PostalCode), customer.PostalCode);
        }
        var optInChanged = false;
        if (data.MarketingOptIn.HasValue) {
            optInChanged = SetBool(value => customer.MarketingOptIn = value, data.MarketingOptIn.Value, customer.MarketingOptIn);
        }

        if (profileChanged || optInChanged) {
            var tenantCode = customer.Tenant?.Code ?? string.Empty;
            if (optInChanged) {
                customer.Version += 1;
                _db.OutboxMessages.Add(CustomerOutboxFactory.OptInChanged(customer, tenantCode));
            }

            if (profileChanged) {
                customer.Version += 1;
                _db.OutboxMessages.Add(CustomerOutboxFactory.ProfileUpdated(customer, tenantCode));
            }
        }

        await _db.SaveChangesAsync(ct);
        return credential;
    }

    public async Task<Credential?> AcceptAgreementsAsync(string normalizedLogin, string? version, bool marketingOptIn, CancellationToken ct) {
        return await ExecuteCustomerWriteWithRetryAsync(
            () => AcceptAgreementsCoreAsync(normalizedLogin, version, marketingOptIn, ct));
    }

    private async Task<Credential?> AcceptAgreementsCoreAsync(string normalizedLogin, string? version, bool marketingOptIn, CancellationToken ct) {
        var credential = await GetCustomerCredentialAsync(normalizedLogin, ct);
        if (credential == null) {
            return null;
        }

        var acceptedAt = DateTime.UtcNow;
        credential.AgreementsAcceptedAtUtc = acceptedAt;
        credential.AgreementsVersion = NormalizeAgreementsVersion(version);

        var customer = credential.Customer;
        if (customer != null) {
            var optInChanged = SetBool(value => customer.MarketingOptIn = value, marketingOptIn, customer.MarketingOptIn);
            var tenantCode = customer.Tenant?.Code ?? string.Empty;
            if (optInChanged) {
                customer.Version += 1;
                _db.OutboxMessages.Add(CustomerOutboxFactory.OptInChanged(customer, tenantCode));
            }

            customer.Version += 1;
            _db.OutboxMessages.Add(CustomerOutboxFactory.AgreementsAccepted(customer, tenantCode, credential.AgreementsVersion, acceptedAt));
        }

        await _db.SaveChangesAsync(ct);
        return credential;
    }

    public async Task<IReadOnlyList<CustomerListItemResponse>> ListByTenantAsync(
        Ulid tenantId, string? search, int skip, int take, CancellationToken ct) {
        var query = _db.Customers
            .AsNoTracking()
            .Include(c => c.Credentials)
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.DisplayName != null && c.DisplayName.ToLower().Contains(term)) ||
                (c.FullName != null && c.FullName.ToLower().Contains(term)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(term)));
        }

        var customers = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return customers.Select(c => new CustomerListItemResponse(
            c.Id.ToString(),
            c.Credentials.Select(cr => cr.Login).FirstOrDefault(),
            c.DisplayName,
            c.FullName,
            c.Email,
            c.PhoneNumber,
            c.City,
            c.MarketingOptIn,
            c.CreatedAtUtc,
            // IsActive: bir credential aktifse customer da aktif kabul edilir. Hic credential
            // yoksa (rare) aktif say — eski davranisla uyumlu, "yeni musteri henuz pasif" gibi
            // anlamsiz UX vermez.
            c.Credentials.Count == 0 || c.Credentials.Any(cr => cr.IsActive)
        )).ToList();
    }

    public async Task<bool?> SetActiveAsync(Ulid tenantId, Ulid customerId, bool active, CancellationToken ct) {
        // Customer entity'sinde IsActive yok — aktif/pasif durumu linked Credential.IsActive
        // uzerinden tasinir. Ortak Credential paylasimi (Customer + PlatformUser/TenantUser)
        // varsa: bu endpoint sadece customer'in kendi salt-customer credential'larini etkiler.
        // Yani PlatformUser/TenantUser FK'si dolu olan credential'lar dokunulmaz; staff'in
        // erisimi musteri pasifle baglantili olarak kapanmaz.
        var customer = await _db.Customers
            .Include(c => c.Credentials)
            .FirstOrDefaultAsync(c => c.Id == customerId && c.TenantId == tenantId, ct);
        if (customer == null) {
            return null;
        }

        foreach (var cred in customer.Credentials) {
            if (cred.PlatformUserId.HasValue || cred.TenantUserId.HasValue) {
                continue;
            }
            cred.IsActive = active;
        }

        await _db.SaveChangesAsync(ct);
        return active;
    }

    public async Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomerSummariesAsync(IReadOnlyCollection<Ulid> customerIds, CancellationToken ct) {
        if (customerIds.Count == 0) {
            return Array.Empty<CustomerSummaryResponse>();
        }

        return await _db.Credentials
            .AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.OwnerType == CredentialOwnerType.Customer && c.CustomerId.HasValue && customerIds.Contains(c.CustomerId.Value))
            .Select(c => new CustomerSummaryResponse(
                c.CustomerId!.Value.ToString(),
                c.Login,
                c.Customer != null ? (c.Customer.Email ?? c.Email) : c.Email,
                c.Customer != null ? c.Customer.DisplayName : null,
                c.Customer != null ? c.Customer.FullName : null
            ))
            .ToListAsync(ct);
    }

    private static bool SetIfProvided(string? requested, Action<string?> assign, string? current) {
        if (requested == null) {
            return false;
        }

        return SetNullable(assign, requested.Trim(), current);
    }

    private static bool SetNullable(Action<string?> assign, string? requested, string? current) {
        if (string.Equals(requested, current, StringComparison.Ordinal)) {
            return false;
        }

        assign(requested);
        return true;
    }

    private static bool SetBool(Action<bool> assign, bool requested, bool current) {
        if (requested == current) {
            return false;
        }

        assign(requested);
        return true;
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeAgreementsVersion(string? version)
        => string.IsNullOrWhiteSpace(version) ? "2025-11" : version.Trim();

    private async Task<T?> ExecuteCustomerWriteWithRetryAsync<T>(Func<Task<T?>> action) {
        for (var attempt = 1; attempt <= MaxConcurrencyRetries; attempt++) {
            try {
                return await action();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries) {
                _db.ChangeTracker.Clear();
            }
        }

        return await action();
    }
}
