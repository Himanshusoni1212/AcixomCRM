namespace AcxiomCRM.Models;

// Lead status machine: New, Contacted, Qualified, Unqualified, Converted, Lost.
// Converted is terminal and only reachable via Convert (Qualified -> Converted).
public static class LeadTransitions
{
    public static readonly string[] Statuses =
        ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"];

    private static readonly Dictionary<string, HashSet<string>> Allowed = new()
    {
        ["New"] = ["New", "Contacted", "Unqualified", "Lost"],
        ["Contacted"] = ["Contacted", "Qualified", "Unqualified", "Lost"],
        ["Qualified"] = ["Qualified", "Converted", "Unqualified", "Lost"],
        ["Unqualified"] = ["Unqualified"],
        ["Converted"] = ["Converted"],
        ["Lost"] = ["Lost"],
    };

    public static bool IsValidStatus(string? status) =>
        status != null && Statuses.Contains(status);

    public static bool CanTransition(string from, string to)
    {
        if (from == to) return true;
        // Converted only via Convert action, never via plain edit.
        if (to == "Converted") return false;
        return Allowed.TryGetValue(from, out var next) && next.Contains(to);
    }

    public static IEnumerable<string> AllowedFrom(string from) =>
        Allowed.TryGetValue(from, out var next) ? next.Where(s => s != "Converted") : [];
}
