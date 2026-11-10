using System.Security.Claims;
using Dapper;
using Jackstaff.Api.Infrastructure;

namespace Jackstaff.Api.Modules.Inventory;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").WithTags("Inventory");

        group.MapGet("/sites", async (ClaimsPrincipal user, TenantDbSession db, CancellationToken ct) =>
        {
            var tenantId = TenantClaims.TenantId(user);
            var rows = await db.Connection.QueryAsync<SiteDto>(new CommandDefinition("""
                SELECT s.id, s.name, s.code, s.site_type, s.address, s.is_active,
                       COUNT(r.id)::int AS radio_count
                FROM sites s
                LEFT JOIN radios r ON r.tenant_id = s.tenant_id AND r.site_id = s.id
                WHERE s.tenant_id = @TenantId
                GROUP BY s.id
                ORDER BY s.name;
                """, new { TenantId = tenantId }, db.Transaction, cancellationToken: ct));
            return Results.Ok(rows);
        }).RequireAuthorization("sites.read");

        group.MapPost("/sites", async (CreateSiteRequest request, ClaimsPrincipal user,
            TenantDbSession db, AuditWriter audit, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.SiteType))
                return Results.BadRequest(new { error = "Nombre, código y tipo de instalación son obligatorios." });
            var tenantId = TenantClaims.TenantId(user);
            var id = Guid.NewGuid();
            await db.Connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO sites (id, tenant_id, name, code, site_type, address)
                VALUES (@Id, @TenantId, @Name, @Code, @SiteType, @Address);
                """, new { Id = id, TenantId = tenantId, request.Name, Code = request.Code.Trim().ToUpperInvariant(),
                    SiteType = request.SiteType.Trim(), Address = NullIfBlank(request.Address) },
                db.Transaction, cancellationToken: ct));
            await audit.WriteAsync(tenantId, TenantClaims.UserId(user), "site.created", "site", id, ct);
            return Results.Created($"/api/sites/{id}", new { id });
        }).RequireAuthorization("sites.write");

        group.MapGet("/radio-models", async (ClaimsPrincipal user, TenantDbSession db, CancellationToken ct) =>
        {
            var rows = await db.Connection.QueryAsync<RadioModelDto>(new CommandDefinition("""
                SELECT id, manufacturer, model, family
                FROM radio_models WHERE tenant_id = @TenantId AND is_active = TRUE
                ORDER BY manufacturer, model;
                """, new { TenantId = TenantClaims.TenantId(user) }, db.Transaction, cancellationToken: ct));
            return Results.Ok(rows);
        }).RequireAuthorization("radios.read");

        group.MapGet("/radios", async (ClaimsPrincipal user, TenantDbSession db, CancellationToken ct) =>
        {
            var rows = await db.Connection.QueryAsync<RadioDto>(new CommandDefinition("""
                SELECT r.id, r.asset_tag, r.serial_number, r.alias, m.manufacturer, m.model,
                       r.status, r.battery_percent, r.last_seen_at, r.site_id, s.name AS site_name,
                       r.radio_model_id,
                       og.name AS group_name, og.id AS group_id
                FROM radios r
                JOIN radio_models m ON m.tenant_id = r.tenant_id AND m.id = r.radio_model_id
                JOIN sites s ON s.tenant_id = r.tenant_id AND s.id = r.site_id
                LEFT JOIN radio_group_assignments a ON a.tenant_id = r.tenant_id
                    AND a.radio_id = r.id AND a.ended_at IS NULL
                LEFT JOIN operational_groups og ON og.tenant_id = a.tenant_id AND og.id = a.group_id
                WHERE r.tenant_id = @TenantId
                ORDER BY r.asset_tag;
                """, new { TenantId = TenantClaims.TenantId(user) }, db.Transaction, cancellationToken: ct));
            return Results.Ok(rows);
        }).RequireAuthorization("radios.read");

        group.MapPost("/radios", async (CreateRadioRequest request, ClaimsPrincipal user,
            TenantDbSession db, AuditWriter audit, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AssetTag) || string.IsNullOrWhiteSpace(request.SerialNumber) ||
                string.IsNullOrWhiteSpace(request.Alias))
                return Results.BadRequest(new { error = "Etiqueta, número de serie y nombre de radio son obligatorios." });

            var tenantId = TenantClaims.TenantId(user);
            var valid = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (SELECT 1 FROM sites WHERE tenant_id = @TenantId AND id = @SiteId AND is_active = TRUE)
                   AND EXISTS (SELECT 1 FROM radio_models WHERE tenant_id = @TenantId AND id = @ModelId AND is_active = TRUE)
                   AND (@GroupId::uuid IS NULL OR EXISTS (
                       SELECT 1 FROM operational_groups WHERE tenant_id = @TenantId AND id = @GroupId AND is_active = TRUE));
                """, new { TenantId = tenantId, SiteId = request.SiteId, ModelId = request.RadioModelId,
                    GroupId = request.InitialGroupId }, db.Transaction, cancellationToken: ct));
            if (!valid) return Results.BadRequest(new { error = "La instalación, modelo o grupo seleccionado no existe en esta organización." });

            var status = (request.InitialStatus ?? "offline").Trim().ToLowerInvariant();
            if (status is not ("online" or "offline" or "maintenance"))
                return Results.BadRequest(new { error = "Estado inicial no válido." });

            var id = Guid.NewGuid();
            await db.Connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO radios (id, tenant_id, radio_model_id, site_id, asset_tag, serial_number, alias, status)
                VALUES (@Id, @TenantId, @ModelId, @SiteId, @AssetTag, @SerialNumber, @Alias, @Status);
                """, new { Id = id, TenantId = tenantId, ModelId = request.RadioModelId,
                    SiteId = request.SiteId, AssetTag = request.AssetTag.Trim(),
                    SerialNumber = request.SerialNumber.Trim(), Alias = request.Alias.Trim(), Status = status },
                db.Transaction, cancellationToken: ct));

            if (request.InitialGroupId is Guid initialGroupId)
                await db.Connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO radio_group_assignments (id, tenant_id, radio_id, group_id, assigned_by)
                    VALUES (@Id, @TenantId, @RadioId, @GroupId, @UserId);
                    """, new { Id = Guid.NewGuid(), TenantId = tenantId, RadioId = id,
                        GroupId = initialGroupId, UserId = TenantClaims.UserId(user) }, db.Transaction, cancellationToken: ct));

            await audit.WriteAsync(tenantId, TenantClaims.UserId(user), "radio.created", "radio", id, ct);
            return Results.Created($"/api/radios/{id}", new { id });
        }).RequireAuthorization("radios.write");

        group.MapGet("/groups", async (ClaimsPrincipal user, TenantDbSession db, CancellationToken ct) =>
        {
            var rows = await db.Connection.QueryAsync<OperationalGroupDto>(new CommandDefinition("""
                SELECT g.id, g.name, g.description, g.is_active,
                       COUNT(a.radio_id)::int AS radio_count
                FROM operational_groups g
                LEFT JOIN radio_group_assignments a ON a.tenant_id = g.tenant_id
                    AND a.group_id = g.id AND a.ended_at IS NULL
                WHERE g.tenant_id = @TenantId
                GROUP BY g.id
                ORDER BY g.name;
                """, new { TenantId = TenantClaims.TenantId(user) }, db.Transaction, cancellationToken: ct));
            return Results.Ok(rows);
        }).RequireAuthorization("groups.read");

        group.MapPost("/groups", async (CreateGroupRequest request, ClaimsPrincipal user,
            TenantDbSession db, AuditWriter audit, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest(new { error = "El nombre del grupo es obligatorio." });
            var tenantId = TenantClaims.TenantId(user);
            var id = Guid.NewGuid();
            await db.Connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO operational_groups (id, tenant_id, name, description)
                VALUES (@Id, @TenantId, @Name, @Description);
                """, new { Id = id, TenantId = tenantId, Name = request.Name.Trim(),
                    Description = NullIfBlank(request.Description) }, db.Transaction, cancellationToken: ct));
            await audit.WriteAsync(tenantId, TenantClaims.UserId(user), "group.created", "operational_group", id, ct);
            return Results.Created($"/api/groups/{id}", new { id });
        }).RequireAuthorization("groups.write");

        return endpoints;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
