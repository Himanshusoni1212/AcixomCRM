namespace AcxiomCRM.Models;

public static class ActivityTypes
{
    public static readonly string[] Types = ["Call", "Meeting", "Email", "Task"];
    public static readonly string[] Statuses = ["Planned", "In Progress", "Completed", "Cancelled"];

    public static bool IsValidType(string? t) => t != null && Types.Contains(t);
    public static bool IsValidStatus(string? s) => s != null && Statuses.Contains(s);
}
