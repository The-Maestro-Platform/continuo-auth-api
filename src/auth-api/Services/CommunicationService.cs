using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class CommunicationService {
    private readonly AuthDbContext _db;

    public CommunicationService(AuthDbContext db) {
        _db = db;
    }

    public async Task<CommunicationDto?> GetAsync(CommunicationOwnerType ownerType, Ulid ownerId, CancellationToken ct) {
        var info = await EnsureInfoAsync(ownerType, ownerId, ct);
        if (info == null) {
            return null;
        }

        return Map(info);
    }

    public async Task<CommunicationDto?> UpsertAddressesAsync(CommunicationOwnerType ownerType, Ulid ownerId, IReadOnlyCollection<AddressDto> addresses, CancellationToken ct) {
        var info = await EnsureInfoAsync(ownerType, ownerId, ct);
        if (info == null) {
            return null;
        }

        var current = info.Addresses.ToDictionary(a => a.Id, a => a);
        var incomingIds = new HashSet<Ulid>();

        foreach (var dto in addresses) {
            ContactAddress? entity = null;
            if (!string.IsNullOrWhiteSpace(dto.Id) && Ulid.TryParse(dto.Id, out var parsed) && current.TryGetValue(parsed, out var existing)) {
                entity = existing;
                incomingIds.Add(parsed);
            }

            if (entity == null) {
                entity = new ContactAddress {
                    CommunicationInfoId = info.Id
                };
                info.Addresses.Add(entity);
            }

            incomingIds.Add(entity.Id);

            entity.Label = dto.Label?.Trim();
            entity.Type = dto.Type;
            entity.Line1 = dto.Line1.Trim();
            entity.Line2 = string.IsNullOrWhiteSpace(dto.Line2) ? null : dto.Line2.Trim();
            entity.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
            entity.Country = string.IsNullOrWhiteSpace(dto.Country) ? null : dto.Country.Trim();
            entity.PostalCode = string.IsNullOrWhiteSpace(dto.PostalCode) ? null : dto.PostalCode.Trim();
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            entity.IsPrimary = dto.IsPrimary;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        var orphaned = info.Addresses.Where(a => !incomingIds.Contains(a.Id)).ToList();
        if (orphaned.Count > 0 && addresses != null) {
            _db.ContactAddresses.RemoveRange(orphaned);
        }

        info.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(info);
    }

    public async Task<CommunicationDto?> UpsertPhonesAsync(CommunicationOwnerType ownerType, Ulid ownerId, IReadOnlyCollection<PhoneDto> phones, CancellationToken ct) {
        var info = await EnsureInfoAsync(ownerType, ownerId, ct);
        if (info == null) {
            return null;
        }

        var current = info.Phones.ToDictionary(p => p.Id, p => p);
        var incomingIds = new HashSet<Ulid>();

        foreach (var dto in phones) {
            ContactPhone? entity = null;
            if (!string.IsNullOrWhiteSpace(dto.Id) && Ulid.TryParse(dto.Id, out var parsed) && current.TryGetValue(parsed, out var existing)) {
                entity = existing;
                incomingIds.Add(parsed);
            }

            if (entity == null) {
                entity = new ContactPhone {
                    CommunicationInfoId = info.Id
                };
                info.Phones.Add(entity);
            }

            incomingIds.Add(entity.Id);

            entity.Type = dto.Type;
            entity.CountryCode = string.IsNullOrWhiteSpace(dto.CountryCode) ? null : dto.CountryCode.Trim();
            entity.Number = dto.Number.Trim();
            entity.Extension = string.IsNullOrWhiteSpace(dto.Extension) ? null : dto.Extension.Trim();
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            entity.IsPrimary = dto.IsPrimary;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        var orphaned = info.Phones.Where(p => !incomingIds.Contains(p.Id)).ToList();
        if (orphaned.Count > 0 && phones != null) {
            _db.ContactPhones.RemoveRange(orphaned);
        }

        info.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Map(info);
    }

    private async Task<CommunicationInfo?> EnsureInfoAsync(CommunicationOwnerType ownerType, Ulid ownerId, CancellationToken ct) {
        IQueryable<CommunicationInfo> query = _db.CommunicationInfos
            .Include(c => c.Addresses)
            .Include(c => c.Phones)
            .AsSplitQuery();

        query = ownerType == CommunicationOwnerType.PlatformUser
            ? query.Where(ci => ci.PlatformUserId == ownerId)
            : query.Where(ci => ci.TenantUserId == ownerId);

        var info = await query.FirstOrDefaultAsync(ct);
        if (info != null) {
            return info;
        }

        if (ownerType == CommunicationOwnerType.PlatformUser) {
            var userExists = await _db.PlatformUsers.AnyAsync(p => p.Id == ownerId, ct);
            if (!userExists) {
                return null;
            }

            info = new CommunicationInfo { OwnerType = ownerType, PlatformUserId = ownerId };
        }
        else {
            var userExists = await _db.TenantUsers.AnyAsync(p => p.Id == ownerId, ct);
            if (!userExists) {
                return null;
            }

            info = new CommunicationInfo { OwnerType = ownerType, TenantUserId = ownerId };
        }

        _db.CommunicationInfos.Add(info);
        await _db.SaveChangesAsync(ct);
        return await _db.CommunicationInfos
            .Include(c => c.Addresses)
            .Include(c => c.Phones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ci => ci.Id == info.Id, ct);
    }

    private static CommunicationDto Map(CommunicationInfo info) {
        return new CommunicationDto {
            OwnerId = info.OwnerType == CommunicationOwnerType.PlatformUser ? info.PlatformUserId!.Value.ToString() : info.TenantUserId!.Value.ToString(),
            OwnerType = info.OwnerType,
            Addresses = info.Addresses
                .OrderByDescending(a => a.IsPrimary)
                .ThenBy(a => a.CreatedAtUtc)
                .Select(a => new AddressDto {
                    Id = a.Id.ToString(),
                    Label = a.Label,
                    Type = a.Type,
                    Line1 = a.Line1,
                    Line2 = a.Line2,
                    City = a.City,
                    Country = a.Country,
                    PostalCode = a.PostalCode,
                    Notes = a.Notes,
                    IsPrimary = a.IsPrimary
                }).ToList(),
            Phones = info.Phones
                .OrderByDescending(p => p.IsPrimary)
                .ThenBy(p => p.CreatedAtUtc)
                .Select(p => new PhoneDto {
                    Id = p.Id.ToString(),
                    Type = p.Type,
                    CountryCode = p.CountryCode,
                    Number = p.Number,
                    Extension = p.Extension,
                    Notes = p.Notes,
                    IsPrimary = p.IsPrimary
                }).ToList()
        };
    }
}
