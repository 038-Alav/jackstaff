using System.Security.Claims;
using Dapper;
using Jackstaff.Api.Infrastructure;
using Jackstaff.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Npgsql;

namespace Jackstaff.Api.Modules.Auth;

public static class AuthEndpoints
{
    private const string MembershipSql = """
        SELECT t.id AS tenant_id, t.name AS tenant_name, t.slug,
               r.name AS role,
               COALESCE(array_agg(DISTINCT p.code) FILTER (WHERE p.code IS NOT NULL), ARRAY[]::text[]) AS permissions
        FROM tenant_memberships tm
        JOIN tenants t ON t.id = tm.tenant_id AND t.is_active = TRUE
        JOIN roles r ON r.id = tm.role_id
        LEFT JOIN role_permissions rp ON rp.role_id = r.id
        LEFT JOIN permissions p ON p.id = rp.permission_id
        WHERE tm.user_id = @UserId AND tm.is_active = TRUE
        GROUP BY t.id, t.name, t.slug, r.name
        ORDER BY CASE WHEN t.slug = 'metro-demo' THEN 0 ELSE 1 END, t.name;
        """;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Identity");

        auth.MapPost("/login", async (LoginRequest request, DatabaseOptions db, PasswordHasher hasher,
            TokenService tokens, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Email y contraseña son obligatorios." });

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE lower(email) = lower(@Email) AND is_active = TRUE;",
                new { request.Email }, cancellationToken: ct));

            if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = user.Id }, cancellationToken: ct))).ToList();
            if (memberships.Count == 0)
                return Results.Unauthorized();

            var active = request.TenantSlug is null
                ? memberships[0]
                : memberships.FirstOrDefault(item => string.Equals(item.Slug, request.TenantSlug, StringComparison.OrdinalIgnoreCase));
            if (active is null)
                return Results.BadRequest(new { error = "La cuenta no pertenece a esa organización." });

            return Results.Ok(BuildResponse(user, active, memberships, tokens));
        }).AllowAnonymous();

        auth.MapPost("/switch-tenant", async (SwitchTenantRequest request, ClaimsPrincipal principal,
            DatabaseOptions db, TokenService tokens, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
                return Results.Unauthorized();

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE id = @UserId AND is_active = TRUE;",
                new { UserId = userId }, cancellationToken: ct));
            if (user is null) return Results.Unauthorized();

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = userId }, cancellationToken: ct))).ToList();
            var active = memberships.FirstOrDefault(item => string.Equals(item.Slug, request.TenantSlug, StringComparison.OrdinalIgnoreCase));
            if (active is null)
                return Results.Forbid();

            return Results.Ok(BuildResponse(user, active, memberships, tokens));
        }).RequireAuthorization();

        auth.MapGet("/me", async (ClaimsPrincipal principal, DatabaseOptions db, TokenService tokens,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ||
                !Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId))
                return Results.Unauthorized();

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE id = @UserId AND is_active = TRUE;",
                new { UserId = userId }, cancellationToken: ct));
            if (user is null) return Results.Unauthorized();

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = userId }, cancellationToken: ct))).ToList();
            var active = memberships.FirstOrDefault(item => item.TenantId == tenantId);
            if (active is null) return Results.Unauthorized();

            return Results.Ok(BuildResponse(user, active, memberships, tokens));
        }).RequireAuthorization();

        return endpoints;
    }

    private static AuthSessionResponse BuildResponse(AuthUser user, TenantMembership active,
        IReadOnlyList<TenantMembership> memberships, TokenService tokens)
    {
        var envelope = tokens.Create(user, active);
        return new AuthSessionResponse(
            envelope.AccessToken,
            envelope.ExpiresAtUtc,
            new UserSummary(user.Id, user.Email, user.Name),
            new TenantSummary(active.TenantId, active.TenantName, active.Slug, active.Role),
            memberships.Select(item => new TenantSummary(item.TenantId, item.TenantName, item.Slug, item.Role)).ToArray());
    }
}
