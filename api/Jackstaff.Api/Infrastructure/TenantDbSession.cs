using Dapper;
using Npgsql;

namespace Jackstaff.Api.Infrastructure;

/// <summary>
/// One PostgreSQL connection and transaction per authenticated HTTP request.
/// Tenant and platform access modes are transaction-local and cannot leak through Npgsql pooling.
/// </summary>
public sealed class TenantDbSession(DatabaseOptions options) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public NpgsqlConnection Connection => _connection
        ?? throw new InvalidOperationException("Tenant database session has not started.");

    public NpgsqlTransaction Transaction => _transaction
        ?? throw new InvalidOperationException("Tenant database transaction has not started.");

    public async Task BeginAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await BeginTransactionAsync(cancellationToken);
        await _connection!.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.tenant_id', @TenantId, true), set_config('app.is_platform_staff', 'false', true);",
            new { TenantId = tenantId.ToString() }, _transaction, cancellationToken: cancellationToken));
    }

    public async Task BeginPlatformAsync(CancellationToken cancellationToken)
    {
        await BeginTransactionAsync(cancellationToken);
        await _connection!.ExecuteAsync(new CommandDefinition(
            "SELECT set_config('app.tenant_id', '', true), set_config('app.is_platform_staff', 'true', true);",
            transaction: _transaction, cancellationToken: cancellationToken));
    }

    private async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            throw new InvalidOperationException("Tenant database session already started.");

        _connection = new NpgsqlConnection(options.ConnectionString);
        await _connection.OpenAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
            await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
            await _transaction.RollbackAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
