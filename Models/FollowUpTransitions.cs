namespace AcxiomCRM.Models;

// Follow-up statuses: Planned, Completed, Missed, Cancelled.
// Planned may move to any terminal state; terminal states are final (same-value edits allowed).
public static class FollowUpTransitions
{
    public static readonly string[] Statuses = ["Planned", "Completed", "Missed", "Cancelled"];
    public static readonly string[] RelatedTypes = ["Customer", "Lead", "Opportunity"];
    public static readonly string[] FollowUpTypes = ["Call", "Meeting", "Email", "Task"];

    public static bool IsValidStatus(string? s) => s != null && Statuses.Contains(s);
    public static bool IsValidRelatedType(string? t) => t != null && RelatedTypes.Contains(t);

    public static bool CanTransition(string from, string to)
    {
        if (from == to) return true;
        return from == "Planned" && (to == "Completed" || to == "Missed" || to == "Cancelled");
    }

    public static string? TransitionError(string from, string to)
    {
        if (!IsValidStatus(to)) return "Follow-up status is required.";
        if (!CanTransition(from, to)) return $"Invalid status transition from {from} to {to}.";
        return null;
    }
}
