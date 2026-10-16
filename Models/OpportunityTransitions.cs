namespace AcxiomCRM.Models;

// Pipeline: Qualification -> Proposal -> Negotiation -> Won/Lost. Lost reachable from any open stage.
public static class OpportunityTransitions
{
    public static readonly string[] Stages =
        ["Qualification", "Proposal", "Negotiation", "Won", "Lost"];

    private static readonly Dictionary<string, HashSet<string>> Allowed = new()
    {
        ["Qualification"] = ["Qualification", "Proposal", "Lost"],
        ["Proposal"] = ["Proposal", "Negotiation", "Lost"],
        ["Negotiation"] = ["Negotiation", "Won", "Lost"],
        ["Won"] = ["Won"],
        ["Lost"] = ["Lost"],
    };

    public static bool IsValidStage(string? stage) =>
        stage != null && Stages.Contains(stage);

    public static bool CanMove(string from, string to)
    {
        if (from == to) return true;
        return Allowed.TryGetValue(from, out var next) && next.Contains(to);
    }

    public static string? MoveError(string from, string to)
    {
        if (!IsValidStage(to)) return "Invalid stage.";
        if (!CanMove(from, to)) return $"Invalid stage transition from {from} to {to}.";
        return null;
    }

    public static IEnumerable<string> AllowedFrom(string from) =>
        Allowed.TryGetValue(from, out var next) ? next : [];
}
