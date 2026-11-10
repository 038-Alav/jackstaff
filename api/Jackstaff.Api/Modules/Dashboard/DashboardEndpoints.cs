using System.Security.Claims;
using Dapper;
using Jackstaff.Api.Infrastructure;

namespace Jackstaff.Api.Modules.Dashboard;

public sealed record DashboardSummary(int TotalRadios, int OnlineRadios, int OfflineRadios,
    int MaintenanceRadios, int ActiveSites, int ActiveGroups, int OpenTickets, int CriticalTickets);
public sealed record DashboardTicket(Guid Id, string TicketNumber, string Title, string Type,
    string Priority, string Status, DateTime CreatedAt, string? RadioAlias);

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard/summary", async (ClaimsPrincipal user, TenantDbSession db, CancellationToken ct) =>
        {
            var summary = await db.Connection.QuerySingleAsync<DashboardSummary>(new CommandDefinition("""
                SELECT
                    (SELECT COUNT(*)::int FROM radios WHERE tenant_id = @TenantId) AS total_radios,
                    (SELECT COUNT(*)::int FROM radios WHERE tenant_id = @TenantId AND status = 'online') AS online_radios,
                    (SELECT COUNT(*)::int FROM radios WHERE tenant_id = @TenantId AND status = 'offline') AS offline_radios,
                    (SELECT COUNT(*)::int FROM radios WHERE tenant_id = @TenantId AND status = 'maintenance') AS maintenance_radios,
                    (SELECT COUNT(*)::int FROM sites WHERE tenant_id = @TenantId AND is_active = TRUE) AS active_sites,
                    (SELECT COUNT(*)::int FROM operational_groups WHERE tenant_id = @TenantId AND is_active = TRUE) AS active_groups,
                    (SELECT COUNT(*)::int FROM tickets WHERE tenant_id = @TenantId AND status NOT IN ('resolved','rejected')) AS open_tickets,
                    (SELECT COUNT(*)::int FROM tickets WHERE tenant_id = @TenantId AND priority = 'critical' AND status NOT IN ('resolved','rejected')) AS critical_tickets;
                """, new { TenantId = TenantClaims.TenantId(user) }, db.Transaction, cancellationToken: ct));

            var recent = await db.Connection.QueryAsync<DashboardTicket>(new CommandDefinition("""
                SELECT t.id, t.ticket_number, t.title, t.type, t.priority, t.status, t.created_at,
                       r.alias AS radio_alias
                FROM tickets t
                LEFT JOIN radios r ON r.tenant_id = t.tenant_id AND r.id = t.radio_id
                WHERE t.tenant_id = @TenantId
                ORDER BY t.created_at DESC LIMIT 6;
                """, new { TenantId = TenantClaims.TenantId(user) }, db.Transaction, cancellationToken: ct));

            return Results.Ok(new { summary, recentTickets = recent });
        }).RequireAuthorization("dashboard.read");

        return endpoints;
    }
}
