using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

// Central ownership rule. Server-side only. Hiding nav is not authorization.
// Admin: all. Manager: team scope (v1 = all team records; narrow to TeamId when added).
// SalesExecutive: only records where OwnerId/AssignedTo == own user id.
public static class RecordScope
{
    public static bool CanAccess(string? ownerId, ClaimsPrincipal user, string currentUserId)
    {
        if (user.IsInRole(Roles.Admin)) return true;
        if (user.IsInRole(Roles.Manager)) return true; // team scope v1
        return ownerId == currentUserId; // SalesExecutive own/assigned only
    }

    public static string CurrentUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
