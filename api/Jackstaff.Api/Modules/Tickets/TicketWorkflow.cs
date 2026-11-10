namespace Jackstaff.Api.Modules.Tickets;

public static class TicketWorkflow
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["open"] = ["in_review", "rejected"],
            ["in_review"] = ["approved", "in_progress", "rejected", "open"],
            ["approved"] = ["in_progress", "rejected"],
            ["in_progress"] = ["resolved", "rejected"]
        };

    public static bool CanTransition(string current, string next) =>
        AllowedTransitions.TryGetValue(current, out var allowed) &&
        allowed.Contains(next, StringComparer.OrdinalIgnoreCase);
}
