namespace Jackstaff.Api.Modules.Integrations.Contracts;

/// <summary>
/// Vendor boundary. Implementations translate an external provider's identifiers and
/// payloads into JACKSTAFF contracts; vendor SDK types must not escape an adapter.
/// </summary>
public interface IRadioProvider
{
    string ProviderKey { get; }
    Task<ProviderRadioSnapshot?> ReadRadioAsync(ProviderRadioReference reference, CancellationToken ct);
}

public interface ILocationProvider
{
    string ProviderKey { get; }
    Task<IReadOnlyList<LocationObservation>> GetRecentLocationsAsync(
        ProviderFleetReference fleet, DateTimeOffset since, CancellationToken ct);
}

public interface IGroupManagementProvider
{
    string ProviderKey { get; }
    Task<GroupChangeResult> RequestGroupChangeAsync(
        ProviderRadioReference radio, ProviderGroupReference group, CancellationToken ct);
}

public interface IDeviceEventProvider
{
    string ProviderKey { get; }
    IAsyncEnumerable<NormalizedDeviceEvent> ReadEventsAsync(
        ProviderFleetReference fleet, DateTimeOffset since, CancellationToken ct);
}

public sealed record ProviderRadioReference(string TenantSlug, string ExternalDeviceId);
public sealed record ProviderFleetReference(string TenantSlug, string ExternalFleetId);
public sealed record ProviderGroupReference(string ExternalGroupId);

public sealed record ProviderRadioSnapshot(
    string ExternalDeviceId,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? DisplayName,
    DateTimeOffset ObservedAt);

/// <summary>
/// A normalized position. Latitude/longitude are WGS84 decimal degrees when present;
/// an indoor provider may instead report a zone identifier.
/// </summary>
public sealed record LocationObservation(
    string ExternalDeviceId,
    DateTimeOffset ObservedAt,
    string Source,
    string PositionType,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    string? ZoneId);

public sealed record GroupChangeResult(
    bool Accepted,
    string? ExternalOperationId,
    string? Message,
    DateTimeOffset? AcceptedAt);

public sealed record NormalizedDeviceEvent(
    string ExternalDeviceId,
    string EventType,
    DateTimeOffset OccurredAt,
    string Source,
    IReadOnlyDictionary<string, string?> Attributes);
