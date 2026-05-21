# continuo-auth-api

> Authentication & user management HTTP service — JWT bearer, BCrypt password hashing, Google SSO, 2FA OTP, password reset, trusted devices.

Service namespace: `AuthApi` (already brand-neutral, kept).
Assembly: `auth-api`. Owns `AuthDbContext` extending `ContinuoDbContext`.

## Dependencies (5 submodules)

- `deps/continuo-shared`
- `deps/continuo-observability`
- `deps/continuo-messaging`
- `deps/continuo-persistence`
- `deps/continuo-configuration`

```bash
git clone --recurse-submodules https://github.com/WhiteToblack/continuo-auth-api.git
cd continuo-auth-api
dotnet build auth-api.sln
```

## Layout

```
src/auth-api/
  Program.cs
  AuthDbContext.cs   # extends ContinuoDbContext
  Contracts/         # API DTOs
  Controllers/       # MVC + Minimal API endpoints
  Data/              # EF Core
  Infrastructure/    # JWT, OTP, BCrypt, Google.Apis.Auth
  Middleware/        # Tenant + auth pipeline
  Migrations/        # EF Core migrations
  Models/            # Domain entities
  Permissions/       # Role/permission/screen models
  Seed/              # Seed data
  Services/          # Business services
  Dockerfile
```

## NuGet

- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0
- `BCrypt.Net-Next` 4.0.3
- `Google.Apis.Auth` 1.68.0
- `Microsoft.EntityFrameworkCore.Design` 10.0

## License

Proprietary — all rights reserved.
