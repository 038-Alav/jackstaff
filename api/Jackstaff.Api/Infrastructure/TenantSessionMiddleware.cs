using System.Security.Claims;
using Dapper;

namespace Jackstaff.Api.Infrastructure;

public sealed class TenantSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantDbSession session)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var workspaceType = context.User.FindFirstValue("workspace_type") ?? "tenant";
        var userClaim = context.User.FindFirstValue("sub");
        if (!Guid.TryParse(userClaim, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (workspaceType == "platform_staff")
        {
            // A platform token must never carry a tenant context as well.
            if (context.User.HasClaim(claim => claim.Type == "tenant_id"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await session.BeginPlatformAsync(context.RequestAborted);
            try
            {
                var membershipIsActive = await session.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM platform_memberships pm
                        JOIN users u ON u.id = pm.user_id
                        WHERE pm.user_id = @UserId
                          AND pm.is_active = TRUE
                          AND u.is_active = TRUE
                    );
                    """,
                    new { UserId = userId }, session.Transaction,
                    cancellationToken: context.RequestAborted));

                if (!membershipIsActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await session.RollbackAsync(CancellationToken.None);
                    return;
                }

                await next(context);
                await session.CommitAsync(context.RequestAborted);
            }
            catch
            {
                await session.RollbackAsync(CancellationToken.None);
                throw;
            }
            return;
        }

        if (workspaceType != "tenant" ||
            !Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await session.BeginAsync(tenantId, context.RequestAborted);
        try
        {
            // Membership and account revocation take effect immediately, not only
            // after the currently issued access token expires.
            var membershipIsActive = await session.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM tenant_memberships tm
                    JOIN tenants t ON t.id = tm.tenant_id
                    JOIN users u ON u.id = tm.user_id
                    WHERE tm.tenant_id = @TenantId
                      AND tm.user_id = @UserId
                      AND tm.is_active = TRUE
                      AND t.is_active = TRUE
                      AND u.is_active = TRUE
                );
                """,
                new { TenantId = tenantId, UserId = userId },
                session.Transaction,
                cancellationToken: context.RequestAborted));

            if (!membershipIsActive)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await session.RollbackAsync(CancellationToken.None);
                return;
            }

            await next(context);
            await session.CommitAsync(context.RequestAborted);
        }
        catch
        {
            await session.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
