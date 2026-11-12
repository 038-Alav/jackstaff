using System.Security.Claims;

namespace Jackstaff.Api.Infrastructure;

public static class TenantClaims
{
    public static Guid TenantId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token does not contain a valid tenant context.");

    public static bool IsPlatformStaff(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue("workspace_type"), "platform_staff", StringComparison.Ordinal);

    public static Guid UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token does not contain a valid user identifier.");
}
