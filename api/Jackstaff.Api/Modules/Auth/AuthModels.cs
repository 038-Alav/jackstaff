namespace Jackstaff.Api.Modules.Auth;

public sealed record LoginRequest(string Email, string Password, string? TenantSlug, string WorkspaceType = "tenant");
public sealed record SwitchTenantRequest(string TenantSlug);
public sealed record AuthUser(Guid Id, string Email, string Name, string PasswordHash);
public sealed record TenantMembership(Guid TenantId, string TenantName, string Slug, string Role, Array Permissions);
public sealed record PlatformMembership(string Role, Array Permissions);
public sealed record LoginWorkspace(string WorkspaceType, string Name, string? TenantSlug);
public sealed record TokenEnvelope(string AccessToken, DateTime ExpiresAtUtc);
public sealed record TenantSummary(Guid Id, string Name, string Slug, string Role);
public sealed record AuthSessionResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string WorkspaceType,
    UserSummary User,
    TenantSummary? ActiveTenant,
    IReadOnlyList<TenantSummary> Tenants);
public sealed record UserSummary(Guid Id, string Email, string Name);
