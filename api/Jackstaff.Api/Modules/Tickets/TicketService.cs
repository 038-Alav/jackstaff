using Dapper;
using Jackstaff.Api.Infrastructure;

namespace Jackstaff.Api.Modules.Tickets;

public sealed class TicketService(TenantDbSession db, AuditWriter audit)
{
    private const string TicketSelect = """
        SELECT t.id, t.ticket_number, t.title, t.description, t.type, t.priority, t.status,
               t.radio_id, r.alias AS radio_alias, t.site_id, s.name AS site_name,
               t.requested_group_id, og.name AS requested_group_name,
               t.created_at, t.updated_at, u.name AS created_by_name,
               t.tenant_id, tenant.name AS tenant_name, tenant.slug AS tenant_slug
        FROM tickets t
        JOIN tenants tenant ON tenant.id = t.tenant_id
        LEFT JOIN radios r ON r.tenant_id = t.tenant_id AND r.id = t.radio_id
        LEFT JOIN sites s ON s.tenant_id = t.tenant_id AND s.id = t.site_id
        LEFT JOIN operational_groups og ON og.tenant_id = t.tenant_id AND og.id = t.requested_group_id
        JOIN users u ON u.id = t.created_by
        """;

    public async Task<IReadOnlyList<TicketDto>> GetAllAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await db.Connection.QueryAsync<TicketDto>(new CommandDefinition(
            TicketSelect + " WHERE t.tenant_id = @TenantId ORDER BY t.created_at DESC;",
            new { TenantId = tenantId }, db.Transaction, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<IReadOnlyList<TicketDto>> GetAllForPlatformAsync(CancellationToken ct)
    {
        var rows = await db.Connection.QueryAsync<TicketDto>(new CommandDefinition(
            TicketSelect + " ORDER BY t.created_at DESC;",
            transaction: db.Transaction, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<IReadOnlyList<TicketCommentDto>?> GetCommentsForPlatformAsync(
        Guid ticketId, CancellationToken ct)
    {
        var tenantId = await FindTicketTenantAsync(ticketId, ct);
        return tenantId is null ? null : await GetCommentsAsync(tenantId.Value, ticketId, ct);
    }

    public async Task<IReadOnlyList<TicketHistoryDto>?> GetHistoryForPlatformAsync(
        Guid ticketId, CancellationToken ct)
    {
        var tenantId = await FindTicketTenantAsync(ticketId, ct);
        return tenantId is null ? null : await GetHistoryAsync(tenantId.Value, ticketId, ct);
    }

    public async Task<(TicketCommentDto? Comment, string? Error, bool NotFound)> AddCommentForPlatformAsync(
        Guid ticketId, Guid userId, string? body, CancellationToken ct)
    {
        var tenantId = await FindTicketTenantAsync(ticketId, ct);
        if (tenantId is null) return (null, null, true);
        return await AddCommentAsync(tenantId.Value, ticketId, userId, body, ct);
    }

    public async Task<(TicketDto? Ticket, string? Error, bool NotFound)> ChangeStatusForPlatformAsync(
        Guid ticketId, Guid userId, string requestedStatus, string? note, CancellationToken ct)
    {
        var tenantId = await FindTicketTenantAsync(ticketId, ct);
        if (tenantId is null) return (null, null, true);
        return await ChangeStatusAsync(tenantId.Value, ticketId, userId, requestedStatus, note, ct);
    }

    private Task<Guid?> FindTicketTenantAsync(Guid ticketId, CancellationToken ct) =>
        db.Connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT tenant_id FROM tickets WHERE id = @TicketId;",
            new { TicketId = ticketId }, db.Transaction, cancellationToken: ct));

    public async Task<IReadOnlyList<TicketCommentDto>?> GetCommentsAsync(
        Guid tenantId, Guid ticketId, CancellationToken ct)
    {
        var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM tickets WHERE tenant_id = @TenantId AND id = @TicketId);",
            new { TenantId = tenantId, TicketId = ticketId }, db.Transaction, cancellationToken: ct));
        if (!exists) return null;

        var rows = await db.Connection.QueryAsync<TicketCommentDto>(new CommandDefinition(
            """
            SELECT c.id, c.ticket_id, c.body, u.name AS author_name, c.created_at
            FROM ticket_comments c
            JOIN users u ON u.id = c.author_id
            WHERE c.tenant_id = @TenantId AND c.ticket_id = @TicketId
            ORDER BY c.created_at, c.id;
            """, new { TenantId = tenantId, TicketId = ticketId },
            db.Transaction, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<IReadOnlyList<TicketHistoryDto>?> GetHistoryAsync(
        Guid tenantId, Guid ticketId, CancellationToken ct)
    {
        var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM tickets WHERE tenant_id = @TenantId AND id = @TicketId);",
            new { TenantId = tenantId, TicketId = ticketId }, db.Transaction, cancellationToken: ct));
        if (!exists) return null;

        var rows = await db.Connection.QueryAsync<TicketHistoryDto>(new CommandDefinition(
            """
            SELECT h.id, h.from_status, h.to_status, h.note, u.name AS actor_name, h.created_at
            FROM ticket_history h
            JOIN users u ON u.id = h.actor_id
            WHERE h.tenant_id = @TenantId AND h.ticket_id = @TicketId
            ORDER BY h.created_at, h.id;
            """, new { TenantId = tenantId, TicketId = ticketId },
            db.Transaction, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<(TicketCommentDto? Comment, string? Error, bool NotFound)> AddCommentAsync(
        Guid tenantId, Guid ticketId, Guid userId, string? body, CancellationToken ct)
    {
        var normalized = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 4000)
            return (null, "El comentario es obligatorio y debe tener como máximo 4.000 caracteres.", false);

        var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM tickets WHERE tenant_id = @TenantId AND id = @TicketId);",
            new { TenantId = tenantId, TicketId = ticketId }, db.Transaction, cancellationToken: ct));
        if (!exists) return (null, null, true);

        var id = Guid.NewGuid();
        await db.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO ticket_comments (id, tenant_id, ticket_id, author_id, body)
            VALUES (@Id, @TenantId, @TicketId, @UserId, @Body);
            """, new { Id = id, TenantId = tenantId, TicketId = ticketId, UserId = userId, Body = normalized },
            db.Transaction, cancellationToken: ct));
        await audit.WriteAsync(tenantId, userId, "ticket.comment.created", "ticket_comment", id, ct);

        var result = await db.Connection.QuerySingleAsync<TicketCommentDto>(new CommandDefinition(
            """
            SELECT c.id, c.ticket_id, c.body, u.name AS author_name, c.created_at
            FROM ticket_comments c
            JOIN users u ON u.id = c.author_id
            WHERE c.tenant_id = @TenantId AND c.id = @Id;
            """, new { TenantId = tenantId, Id = id }, db.Transaction, cancellationToken: ct));
        return (result, null, false);
    }

    public async Task<(TicketDto? Ticket, string? Error)> CreateAsync(Guid tenantId, Guid userId,
        CreateTicketRequest request, CancellationToken ct)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 180)
            return (null, "El título es obligatorio y debe tener como máximo 180 caracteres.");
        var type = Normalize(request.Type);
        var priority = Normalize(request.Priority);
        if (type is not ("group_change" or "incident" or "maintenance" or "other"))
            return (null, "Tipo de solicitud no válido.");
        if (priority is not ("low" or "normal" or "high" or "critical"))
            return (null, "Prioridad no válida.");
        if (type == "group_change" && (request.RadioId is null || request.RequestedGroupId is null))
            return (null, "Para solicitar un cambio de grupo debes seleccionar una radio y el grupo solicitado.");

        if (request.RadioId is Guid radioId)
        {
            var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM radios WHERE tenant_id = @TenantId AND id = @RadioId);",
                new { TenantId = tenantId, RadioId = radioId }, db.Transaction, cancellationToken: ct));
            if (!exists) return (null, "La radio seleccionada no existe en esta organización.");
        }
        if (request.SiteId is Guid siteId)
        {
            var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM sites WHERE tenant_id = @TenantId AND id = @SiteId);",
                new { TenantId = tenantId, SiteId = siteId }, db.Transaction, cancellationToken: ct));
            if (!exists) return (null, "La instalación seleccionada no existe en esta organización.");
        }
        if (request.RequestedGroupId is Guid groupId)
        {
            var exists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM operational_groups WHERE tenant_id = @TenantId AND id = @GroupId AND is_active = TRUE);",
                new { TenantId = tenantId, GroupId = groupId }, db.Transaction, cancellationToken: ct));
            if (!exists) return (null, "El grupo solicitado no existe en esta organización.");
        }

        var id = Guid.NewGuid();
        var ticketNumber = $"JSF-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await db.Connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO tickets (id, tenant_id, ticket_number, title, description, type, priority,
                status, radio_id, site_id, requested_group_id, created_by)
            VALUES (@Id, @TenantId, @TicketNumber, @Title, @Description, @Type, @Priority,
                'open', @RadioId, @SiteId, @RequestedGroupId, @UserId);
            """, new
        {
            Id = id, TenantId = tenantId, TicketNumber = ticketNumber, Title = title,
            Description = request.Description?.Trim() ?? string.Empty, Type = type,
            Priority = priority, RadioId = request.RadioId, SiteId = request.SiteId,
            RequestedGroupId = type == "group_change" ? request.RequestedGroupId : null, UserId = userId
        }, db.Transaction, cancellationToken: ct));

        await AddHistoryAsync(tenantId, id, userId, null, "open", "Solicitud creada", ct);
        await audit.WriteAsync(tenantId, userId, "ticket.created", "ticket", id, ct);
        return (await GetByIdAsync(tenantId, id, ct), null);
    }

    public async Task<(TicketDto? Ticket, string? Error, bool NotFound)> ChangeStatusAsync(
        Guid tenantId, Guid ticketId, Guid userId, string requestedStatus, string? note, CancellationToken ct)
    {
        var next = Normalize(requestedStatus);
        if (next is not ("open" or "in_review" or "approved" or "in_progress" or "resolved" or "rejected"))
            return (null, "Estado solicitado no válido.", false);

        var current = await db.Connection.QuerySingleOrDefaultAsync<TicketState>(new CommandDefinition("""
            SELECT id, ticket_number, type, status, radio_id, requested_group_id
            FROM tickets WHERE tenant_id = @TenantId AND id = @TicketId
            FOR UPDATE;
            """, new { TenantId = tenantId, TicketId = ticketId }, db.Transaction, cancellationToken: ct));
        if (current is null) return (null, null, true);
        if (!TicketWorkflow.CanTransition(current.Status, next))
            return (null, $"No se permite cambiar el estado desde '{current.Status}' a '{next}'.", false);

        if (next == "resolved" && current.Type == "group_change")
        {
            if (current.RadioId is null || current.RequestedGroupId is null)
                return (null, "La solicitud de cambio de grupo no tiene radio o grupo de destino.", false);

            // Serialize concurrent group changes for the same radio without requiring
            // platform staff to receive broad UPDATE access to the radios table. The
            // tenant-scoped partial unique index remains the final database invariant.
            var radioLockKey = $"{tenantId:N}:{current.RadioId.Value:N}";
            await db.Connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtextextended(@LockKey, 0));",
                new { LockKey = radioLockKey }, db.Transaction, cancellationToken: ct));

            var radioExists = await db.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS(SELECT 1 FROM radios WHERE tenant_id = @TenantId AND id = @RadioId);",
                new { TenantId = tenantId, RadioId = current.RadioId }, db.Transaction, cancellationToken: ct));
            if (!radioExists)
                return (null, "La radio asociada ya no existe en esta organización.", false);

            await db.Connection.ExecuteAsync(new CommandDefinition("""
                UPDATE radio_group_assignments SET ended_at = NOW()
                WHERE tenant_id = @TenantId AND radio_id = @RadioId AND ended_at IS NULL;
                """, new { TenantId = tenantId, RadioId = current.RadioId }, db.Transaction, cancellationToken: ct));

            await db.Connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO radio_group_assignments (id, tenant_id, radio_id, group_id, assigned_by)
                VALUES (@Id, @TenantId, @RadioId, @GroupId, @UserId);
                """, new { Id = Guid.NewGuid(), TenantId = tenantId, RadioId = current.RadioId,
                    GroupId = current.RequestedGroupId, UserId = userId }, db.Transaction, cancellationToken: ct));
        }

        await db.Connection.ExecuteAsync(new CommandDefinition("""
            UPDATE tickets SET status = @Status, updated_at = NOW(),
                resolution_note = CASE WHEN @Status = 'resolved' THEN @Note ELSE resolution_note END
            WHERE tenant_id = @TenantId AND id = @TicketId;
            """, new { TenantId = tenantId, TicketId = ticketId, Status = next,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim() }, db.Transaction, cancellationToken: ct));

        await AddHistoryAsync(tenantId, ticketId, userId, current.Status, next,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(), ct);
        await audit.WriteAsync(tenantId, userId, $"ticket.status.{next}", "ticket", ticketId, ct);
        return (await GetByIdAsync(tenantId, ticketId, ct), null, false);
    }

    private Task<TicketDto?> GetByIdAsync(Guid tenantId, Guid ticketId, CancellationToken ct) =>
        db.Connection.QuerySingleOrDefaultAsync<TicketDto>(new CommandDefinition(
            TicketSelect + " WHERE t.tenant_id = @TenantId AND t.id = @TicketId;",
            new { TenantId = tenantId, TicketId = ticketId }, db.Transaction, cancellationToken: ct));

    private Task AddHistoryAsync(Guid tenantId, Guid ticketId, Guid userId, string? from,
        string to, string? note, CancellationToken ct) =>
        db.Connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO ticket_history (id, tenant_id, ticket_id, actor_id, from_status, to_status, note)
            VALUES (@Id, @TenantId, @TicketId, @UserId, @FromStatus, @ToStatus, @Note);
            """, new { Id = Guid.NewGuid(), TenantId = tenantId, TicketId = ticketId,
                UserId = userId, FromStatus = from, ToStatus = to, Note = note },
            db.Transaction, cancellationToken: ct));

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

}
