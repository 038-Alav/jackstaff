namespace Jackstaff.Api.Modules.Inventory;

public sealed record CreateSiteRequest(string Name, string Code, string SiteType, string? Address);
public sealed record CreateGroupRequest(string Name, string? Description);
public sealed record CreateRadioRequest(Guid RadioModelId, Guid SiteId, string AssetTag, string SerialNumber,
    string Alias, string? InitialStatus, Guid? InitialGroupId);
public sealed record SiteDto(Guid Id, string Name, string Code, string SiteType, string? Address,
    bool IsActive, int RadioCount);
public sealed record RadioModelDto(Guid Id, string Manufacturer, string Model, string Family);
public sealed record RadioDto(Guid Id, string AssetTag, string SerialNumber, string Alias, string Manufacturer,
    string Model, string Status, short? BatteryPercent, DateTime? LastSeenAt, Guid SiteId, string SiteName,
    Guid RadioModelId, string? GroupName, Guid? GroupId);
public sealed record OperationalGroupDto(Guid Id, string Name, string? Description, bool IsActive, int RadioCount);
