using System.Security.Claims;
using Dapper;
using Jackstaff.Api.Infrastructure;
using Jackstaff.Api.Infrastructure.Security;
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
        ORDER BY t.name;
        """;

    private const string PlatformMembershipSql = """
        SELECT r.name AS role,
               COALESCE(array_agg(DISTINCT p.code) FILTER (WHERE p.code IS NOT NULL), ARRAY[]::text[]) AS permissions
        FROM platform_memberships pm
        JOIN roles r ON r.id = pm.role_id
        LEFT JOIN role_permissions rp ON rp.role_id = r.id
        LEFT JOIN permissions p ON p.id = rp.permission_id
        WHERE pm.user_id = @UserId AND pm.is_active = TRUE
        GROUP BY r.name;
        """;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Identity");

        // The demo login needs a workspace selector. The names/slugs come from data,
        // not from a hardcoded list in the web client. Replace this discovery flow
        // with a tenant-aware identity flow before exposing a production SaaS login.
        auth.MapGet("/workspaces", async (DatabaseOptions db, CancellationToken ct) =>
        {
            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var workspaces = (await connection.QueryAsync<LoginWorkspace>(new CommandDefinition(
                """
                SELECT 'tenant'::text AS workspace_type, name, slug AS tenant_slug
                FROM tenants WHERE is_active = TRUE
                ORDER BY name;
                """, cancellationToken: ct))).ToList();
            var hasPlatformStaff = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1 FROM platform_memberships pm
                    JOIN users u ON u.id = pm.user_id
                    WHERE pm.is_active = TRUE AND u.is_active = TRUE
                );
                """, cancellationToken: ct));
            if (hasPlatformStaff)
                workspaces.Add(new LoginWorkspace("platform_staff", "Soporte JACKSTAFF", null));
            return Results.Ok(workspaces);
        }).AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, DatabaseOptions db, PasswordHasher hasher,
            TokenService tokens, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return Results.BadRequest(new { error = "Email y contraseña son obligatorios." });

            var workspaceType = request.WorkspaceType?.Trim().ToLowerInvariant();
            if (workspaceType is not ("tenant" or "platform_staff"))
                return Results.BadRequest(new { error = "El espacio de trabajo seleccionado no es válido." });

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE lower(email) = lower(@Email) AND is_active = TRUE;",
                new { request.Email }, cancellationToken: ct));

            if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
                return Results.Unauthorized();

            if (workspaceType == "platform_staff")
            {
                var platformMembership = await connection.QuerySingleOrDefaultAsync<PlatformMembership>(
                    new CommandDefinition(PlatformMembershipSql, new { UserId = user.Id }, cancellationToken: ct));
                if (platformMembership is null) return Results.Forbid();
                return Results.Ok(BuildPlatformResponse(user, platformMembership, tokens));
            }

            if (string.IsNullOrWhiteSpace(request.TenantSlug))
                return Results.BadRequest(new { error = "Selecciona una organización." });

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = user.Id }, cancellationToken: ct))).ToList();
            if (memberships.Count == 0)
                return Results.Unauthorized();

            var active = memberships.FirstOrDefault(item =>
                string.Equals(item.Slug, request.TenantSlug, StringComparison.OrdinalIgnoreCase));
            if (active is null)
                return Results.BadRequest(new { error = "La cuenta no pertenece a esa organización." });

            return Results.Ok(BuildTenantResponse(user, active, memberships, tokens));
        }).AllowAnonymous();

        auth.MapPost("/switch-tenant", async (SwitchTenantRequest request, ClaimsPrincipal principal,
            DatabaseOptions db, TokenService tokens, CancellationToken ct) =>
        {
            if (principal.FindFirstValue("workspace_type") == "platform_staff" ||
                !Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
                return Results.Forbid();

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE id = @UserId AND is_active = TRUE;",
                new { UserId = userId }, cancellationToken: ct));
            if (user is null) return Results.Unauthorized();

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = userId }, cancellationToken: ct))).ToList();
            var active = memberships.FirstOrDefault(item =>
                string.Equals(item.Slug, request.TenantSlug, StringComparison.OrdinalIgnoreCase));
            if (active is null)
                return Results.Forbid();

            return Results.Ok(BuildTenantResponse(user, active, memberships, tokens));
        }).RequireAuthorization();

        auth.MapGet("/me", async (ClaimsPrincipal principal, DatabaseOptions db, TokenService tokens,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
                return Results.Unauthorized();

            await using var connection = new NpgsqlConnection(db.ConnectionString);
            await connection.OpenAsync(ct);
            var user = await connection.QuerySingleOrDefaultAsync<AuthUser>(new CommandDefinition(
                "SELECT id, email, name, password_hash FROM users WHERE id = @UserId AND is_active = TRUE;",
                new { UserId = userId }, cancellationToken: ct));
            if (user is null) return Results.Unauthorized();

            if (principal.FindFirstValue("workspace_type") == "platform_staff")
            {
                var platformMembership = await connection.QuerySingleOrDefaultAsync<PlatformMembership>(
                    new CommandDefinition(PlatformMembershipSql, new { UserId = userId }, cancellationToken: ct));
                return platformMembership is null
                    ? Results.Unauthorized()
                    : Results.Ok(BuildPlatformResponse(user, platformMembership, tokens));
            }

            if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId))
                return Results.Unauthorized();

            var memberships = (await connection.QueryAsync<TenantMembership>(new CommandDefinition(
                MembershipSql, new { UserId = userId }, cancellationToken: ct))).ToList();
            var active = memberships.FirstOrDefault(item => item.TenantId == tenantId);
            if (active is null) return Results.Unauthorized();

            return Results.Ok(BuildTenantResponse(user, active, memberships, tokens));
        }).RequireAuthorization();

        return endpoints;
    }

    private static AuthSessionResponse BuildTenantResponse(AuthUser user, TenantMembership active,
        IReadOnlyList<TenantMembership> memberships, TokenService tokens)
    {
        var envelope = tokens.Create(user, active);
        return new AuthSessionResponse(
            envelope.AccessToken,
            envelope.ExpiresAtUtc,
            "tenant",
            new UserSummary(user.Id, user.Email, user.Name),
            new TenantSummary(active.TenantId, active.TenantName, active.Slug, active.Role),
            memberships.Select(item => new TenantSummary(item.TenantId, item.TenantName, item.Slug, item.Role)).ToArray());
    }

    private static AuthSessionResponse BuildPlatformResponse(AuthUser user, PlatformMembership membership,
        TokenService tokens)
    {
        var envelope = tokens.CreatePlatform(user, membership);
        return new AuthSessionResponse(
            envelope.AccessToken,
            envelope.ExpiresAtUtc,
            "platform_staff",
            new UserSummary(user.Id, user.Email, user.Name),
            null,
            Array.Empty<TenantSummary>());
    }
}
