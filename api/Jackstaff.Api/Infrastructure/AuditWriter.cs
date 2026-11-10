using Dapper;

namespace Jackstaff.Api.Infrastructure;

/// <summary>Persists tenant-scoped audit events inside the caller's request transaction.</summary>
public sealed class AuditWriter(TenantDbSession db)
{
    public Task WriteAsync(Guid tenantId, Guid userId, string action, string entityType,
        Guid entityId, CancellationToken cancellationToken) =>
        db.Connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO audit_logs (id, tenant_id, actor_id, action, entity_type, entity_id)
            VALUES (@Id, @TenantId, @UserId, @Action, @EntityType, @EntityId);
            """, new { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId,
                Action = action, EntityType = entityType, EntityId = entityId },
            db.Transaction, cancellationToken: cancellationToken));
}
