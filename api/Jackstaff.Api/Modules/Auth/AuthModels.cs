namespace Jackstaff.Api.Modules.Auth;

public sealed record LoginRequest(string Email, string Password, string? TenantSlug);
public sealed record SwitchTenantRequest(string TenantSlug);
public sealed record AuthUser(Guid Id, string Email, string Name, string PasswordHash);
public sealed record TenantMembership(Guid TenantId, string TenantName, string Slug, string Role, Array Permissions);
public sealed record TokenEnvelope(string AccessToken, DateTime ExpiresAtUtc);
public sealed record TenantSummary(Guid Id, string Name, string Slug, string Role);
public sealed record AuthSessionResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserSummary User,
    TenantSummary ActiveTenant,
    IReadOnlyList<TenantSummary> Tenants);
public sealed record UserSummary(Guid Id, string Email, string Name);
