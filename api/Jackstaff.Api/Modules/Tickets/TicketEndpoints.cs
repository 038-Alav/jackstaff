using System.Security.Claims;
using Jackstaff.Api.Infrastructure;

namespace Jackstaff.Api.Modules.Tickets;

public static class TicketEndpoints
{
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tickets").WithTags("Service Desk");

        group.MapGet("", async (ClaimsPrincipal user, TicketService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(TenantClaims.TenantId(user), ct)))
            .RequireAuthorization("tickets.read");

        group.MapPost("", async (CreateTicketRequest request, ClaimsPrincipal user,
            TicketService service, CancellationToken ct) =>
        {
            var (ticket, error) = await service.CreateAsync(TenantClaims.TenantId(user),
                TenantClaims.UserId(user), request, ct);
            return error is not null
                ? Results.BadRequest(new { error })
                : Results.Created($"/api/tickets/{ticket!.Id}", ticket);
        }).RequireAuthorization("tickets.write");

        group.MapGet("/{ticketId:guid}/comments", async (Guid ticketId, ClaimsPrincipal user,
            TicketService service, CancellationToken ct) =>
        {
            var comments = await service.GetCommentsAsync(TenantClaims.TenantId(user), ticketId, ct);
            return comments is null ? Results.NotFound(new { error = "No se encontró el ticket." }) : Results.Ok(comments);
        }).RequireAuthorization("tickets.read");

        group.MapGet("/{ticketId:guid}/history", async (Guid ticketId, ClaimsPrincipal user,
            TicketService service, CancellationToken ct) =>
        {
            var history = await service.GetHistoryAsync(TenantClaims.TenantId(user), ticketId, ct);
            return history is null ? Results.NotFound(new { error = "No se encontró el ticket." }) : Results.Ok(history);
        }).RequireAuthorization("tickets.read");

        group.MapPost("/{ticketId:guid}/comments", async (Guid ticketId, AddTicketCommentRequest request,
            ClaimsPrincipal user, TicketService service, CancellationToken ct) =>
        {
            var (comment, error, notFound) = await service.AddCommentAsync(TenantClaims.TenantId(user),
                ticketId, TenantClaims.UserId(user), request.Body, ct);
            if (notFound) return Results.NotFound(new { error = "No se encontró el ticket." });
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Created($"/api/tickets/{ticketId}/comments/{comment!.Id}", comment);
        }).RequireAuthorization("tickets.write");

        group.MapPatch("/{ticketId:guid}/status", async (Guid ticketId, ChangeTicketStatusRequest request,
            ClaimsPrincipal user, TicketService service, CancellationToken ct) =>
        {
            var (ticket, error, notFound) = await service.ChangeStatusAsync(
                TenantClaims.TenantId(user), ticketId, TenantClaims.UserId(user),
                request.Status, request.Note, ct);
            if (notFound) return Results.NotFound(new { error = "No se encontró el ticket." });
            if (error is not null) return Results.BadRequest(new { error });
            return Results.Ok(ticket);
        }).RequireAuthorization("tickets.manage");

        return endpoints;
    }
}
