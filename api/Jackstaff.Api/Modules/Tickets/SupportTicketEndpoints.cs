using System.Security.Claims;
using Jackstaff.Api.Infrastructure;

namespace Jackstaff.Api.Modules.Tickets;

public static class SupportTicketEndpoints
{
    public static IEndpointRouteBuilder MapSupportTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/support/tickets").WithTags("Platform Support");

        group.MapGet("", async (TicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllForPlatformAsync(ct)))
            .RequireAuthorization("platform.tickets.read");

        group.MapGet("/{ticketId:guid}/comments", async (Guid ticketId, TicketService service, CancellationToken ct) =>
        {
            var comments = await service.GetCommentsForPlatformAsync(ticketId, ct);
            return comments is null
                ? Results.NotFound(new { error = "No se encontró el ticket." })
                : Results.Ok(comments);
        }).RequireAuthorization("platform.tickets.read");

        group.MapGet("/{ticketId:guid}/history", async (Guid ticketId, TicketService service, CancellationToken ct) =>
        {
            var history = await service.GetHistoryForPlatformAsync(ticketId, ct);
            return history is null
                ? Results.NotFound(new { error = "No se encontró el ticket." })
                : Results.Ok(history);
        }).RequireAuthorization("platform.tickets.read");

        group.MapPost("/{ticketId:guid}/comments", async (Guid ticketId, AddTicketCommentRequest request,
            ClaimsPrincipal user, TicketService service, CancellationToken ct) =>
        {
            var (comment, error, notFound) = await service.AddCommentForPlatformAsync(
                ticketId, TenantClaims.UserId(user), request.Body, ct);
            if (notFound) return Results.NotFound(new { error = "No se encontró el ticket." });
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Created($"/api/support/tickets/{ticketId}/comments/{comment!.Id}", comment);
        }).RequireAuthorization("platform.tickets.manage");

        group.MapPatch("/{ticketId:guid}/status", async (Guid ticketId, ChangeTicketStatusRequest request,
            ClaimsPrincipal user, TicketService service, CancellationToken ct) =>
        {
            var (ticket, error, notFound) = await service.ChangeStatusForPlatformAsync(
                ticketId, TenantClaims.UserId(user), request.Status, request.Note, ct);
            if (notFound) return Results.NotFound(new { error = "No se encontró el ticket." });
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Ok(ticket);
        }).RequireAuthorization("platform.tickets.manage");

        return endpoints;
    }
}
