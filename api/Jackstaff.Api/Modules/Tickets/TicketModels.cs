namespace Jackstaff.Api.Modules.Tickets;

public sealed record CreateTicketRequest(string Title, string Description, string Type, string Priority,
    Guid? RadioId, Guid? SiteId, Guid? RequestedGroupId);
public sealed record ChangeTicketStatusRequest(string Status, string? Note);
public sealed record TicketDto(Guid Id, string TicketNumber, string Title, string Description, string Type,
    string Priority, string Status, Guid? RadioId, string? RadioAlias, Guid? SiteId, string? SiteName,
    Guid? RequestedGroupId, string? RequestedGroupName, DateTime CreatedAt, DateTime UpdatedAt,
    string CreatedByName, Guid TenantId, string TenantName, string TenantSlug);

public sealed record TicketState(Guid Id, string TicketNumber, string Type, string Status, Guid? RadioId, Guid? RequestedGroupId);

public sealed record TicketCommentDto(Guid Id, Guid TicketId, string Body, string AuthorName, DateTime CreatedAt);
public sealed record AddTicketCommentRequest(string Body);
public sealed record TicketHistoryDto(Guid Id, string? FromStatus, string ToStatus, string? Note, string ActorName, DateTime CreatedAt);
