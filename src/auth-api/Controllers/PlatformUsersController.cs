using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("platform/users")]
[AuthorizeUserType(UserType.PlatformUser)]
public class PlatformUsersController : ControllerBase {
    private readonly AuthDbContext _db;

    public PlatformUsersController(AuthDbContext db) {
        _db = db;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    [RequirePermission("platform.auth.users.view")]
    public async Task<IActionResult> List([FromQuery] int take = 200, CancellationToken ct = default) {
        take = Math.Clamp(take, 1, 500);
        var items = await _db.PlatformUsers
            .AsNoTracking()
            .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
            .Include(u => u.Credentials)
            .OrderBy(u => u.Email)
            .Take(take)
            .Select(u => new {
                id = u.Id.ToString(),
                u.Email,
                u.DisplayName,
                u.IsActive,
                roles = u.Roles.Select(r => r.Role.Name),
                credentials = u.Credentials.Select(c => new { id = c.Id.ToString(), c.Login, c.IsActive })
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [ContinuoProxyMethod("ui")]
    [RequirePermission("platform.auth.users.manage")]
    public async Task<IActionResult> Create([FromBody] CreatePlatformUserRequest req, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password)) {
            return BadRequest("Email and password are required");
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email : req.DisplayName.Trim();

        // Aktif PlatformUser zaten varsa → conflict (ayni mailde iki aktif staff kaydi yok).
        if (await _db.PlatformUsers.AnyAsync(u => u.Email == email && u.IsActive, ct)) {
            return Conflict("User already exists");
        }

        // Pasif PlatformUser varsa: yeni satir acma — eskisini reaktive et + parolayi sifirla.
        // Aksi halde auth'ta ayni mailden iki kayit kaliyor; login query IsActive filtreliyor
        // ama UI'da kafa karistirici listede iki satir + ekleyen kullanici "neden login olmuyorum"
        // diye sasiriyor (eski parola hash'i tutuluyor ama mustChangePassword false ise yeni
        // parola admin tarafindan verilmis sayilmiyor).
        var inactiveUser = await _db.PlatformUsers
            .Include(u => u.Credentials)
            .Where(u => u.Email == email && !u.IsActive)
            .OrderByDescending(u => u.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (inactiveUser != null) {
            inactiveUser.IsActive = true;
            inactiveUser.DisplayName = displayName;
            inactiveUser.UpdatedAtUtc = DateTime.UtcNow;

            var ownCredential = inactiveUser.Credentials
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefault(c => c.Login == email);

            if (ownCredential != null) {
                ownCredential.IsActive = true;
                ownCredential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
                ownCredential.MustChangePassword = true;
                ownCredential.PasswordChangedAtUtc = null;
            }
            else {
                var newCred = new Credential {
                    Login = email,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    OwnerType = CredentialOwnerType.PlatformUser,
                    PlatformUser = inactiveUser,
                    IsActive = true,
                    MustChangePassword = true
                };
                inactiveUser.Credentials.Add(newCred);
                _db.Credentials.Add(newCred);
            }

            if (req.RoleIds is { Length: > 0 }) {
                var existingRoles = await _db.UserRoles.Where(r => r.PlatformUserId == inactiveUser.Id).ToListAsync(ct);
                _db.UserRoles.RemoveRange(existingRoles);

                var roleIds = req.RoleIds
                    .Where(id => Ulid.TryParse(id, out _))
                    .Select(Ulid.Parse)
                    .ToArray();
                var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Platform).ToListAsync(ct);
                foreach (var role in roles) {
                    inactiveUser.Roles.Add(new UserRole { Role = role, PlatformUser = inactiveUser });
                }
            }

            await _db.SaveChangesAsync(ct);

            return Ok(new {
                id = inactiveUser.Id.ToString(),
                inactiveUser.Email,
                inactiveUser.DisplayName,
                reactivated = true
            });
        }

        // Ayni mailde aktif credential olabilir (ornegin Customer signup'tan gelen).
        // Bu durumda yeni credential acmak yerine mevcut credential'a PlatformUser
        // FK'sini iliştir: tek hesap, ortak parola. Yeni parola admin tarafindan
        // verildigi icin parola sifirlanir + MustChangePassword=true.
        var existingActiveCredential = await _db.Credentials
            .FirstOrDefaultAsync(c => c.Login == email && c.IsActive, ct);

        PlatformUser user;
        Credential credential;
        var attachedToExisting = false;

        if (existingActiveCredential != null) {
            if (existingActiveCredential.PlatformUserId.HasValue) {
                // Bu credential zaten bir PlatformUser'a bagli (ama PlatformUser pasif olmali — yukaridaki
                // aktif PU check'i gecmis). Defansif: yine de conflict don.
                return Conflict("User already exists");
            }

            user = new PlatformUser {
                Email = email,
                DisplayName = displayName,
                IsActive = true
            };
            _db.PlatformUsers.Add(user);

            existingActiveCredential.PlatformUser = user;
            existingActiveCredential.OwnerType = CredentialOwnerType.PlatformUser;
            existingActiveCredential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            existingActiveCredential.MustChangePassword = true;
            existingActiveCredential.PasswordChangedAtUtc = null;
            credential = existingActiveCredential;
            attachedToExisting = true;
        }
        else {
            user = new PlatformUser {
                Email = email,
                DisplayName = displayName,
                IsActive = true
            };

            credential = new Credential {
                Login = email,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                OwnerType = CredentialOwnerType.PlatformUser,
                PlatformUser = user,
                IsActive = true,
                MustChangePassword = true
            };

            user.Credentials.Add(credential);
            _db.PlatformUsers.Add(user);
            _db.Credentials.Add(credential);
        }

        if (req.RoleIds is { Length: > 0 }) {
            var roleIds = req.RoleIds
                .Where(id => Ulid.TryParse(id, out _))
                .Select(Ulid.Parse)
                .ToArray();
            var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Platform).ToListAsync(ct);
            foreach (var role in roles) {
                user.Roles.Add(new UserRole { Role = role, PlatformUser = user });
            }
        }

        await _db.SaveChangesAsync(ct);

        return Created($"/platform/users/{user.Id}", new {
            id = user.Id.ToString(),
            user.Email,
            user.DisplayName,
            attachedToExistingCredential = attachedToExisting
        });
    }

    [HttpPatch("{id}/active")]
    [ContinuoProxyMethod("ui")]
    [RequirePermission("platform.auth.users.manage")]
    public async Task<IActionResult> SetActive([FromRoute] string id, [FromBody] SetActiveRequest req, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var user = await _db.PlatformUsers.Include(u => u.Credentials).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) {
            return NotFound();
        }

        user.IsActive = req.Active;
        foreach (var cred in user.Credentials) {
            cred.IsActive = req.Active;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id.ToString(), user.IsActive });
    }

    [HttpPut("{id}/roles")]
    [ContinuoProxyMethod("ui")]
    [RequirePermission("platform.auth.users.manage")]
    public async Task<IActionResult> SetRoles([FromRoute] string id, [FromBody] SetRolesRequest req, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var user = await _db.PlatformUsers.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) {
            return NotFound();
        }

        var incoming = req.RoleIds?.Where(id => Ulid.TryParse(id, out _)).Select(Ulid.Parse).ToArray() ?? Array.Empty<Ulid>();
        var roles = incoming.Length == 0
            ? new List<Role>()
            : await _db.Roles.Where(r => incoming.Contains(r.Id) && r.Scope == RoleScope.Platform).ToListAsync(ct);

        _db.UserRoles.RemoveRange(user.Roles);
        user.Roles.Clear();
        foreach (var role in roles) {
            user.Roles.Add(new UserRole { Role = role, PlatformUser = user });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id.ToString(), roleIds = roles.Select(r => r.Id) });
    }

    [HttpPatch("{id}/password")]
    [ContinuoProxyMethod("ui")]
    [RequirePermission("platform.auth.users.manage")]
    public async Task<IActionResult> ResetPassword([FromRoute] string id, [FromBody] ResetPasswordRequest req, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6) {
            return BadRequest("Password too short");
        }

        var cred = await _db.Credentials.FirstOrDefaultAsync(c => c.PlatformUserId == userId, ct);
        if (cred == null) {
            return NotFound();
        }

        cred.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        cred.MustChangePassword = true;
        cred.PasswordChangedAtUtc = null;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = cred.Id.ToString() });
    }
}
