using AcxiomCRM.Data;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

// Validates RelatedType + RelatedId against records visible to the caller
// and resolves display names. Sales scope: related record must be owned by me.
public static class RelatedRecords
{
    public record Resolved(bool Found, string Name, int? CustomerId, int? LeadId);

    public static async Task<Resolved> ResolveAsync(ApplicationDbContext db, string type, int id, bool isSales, string me)
    {
        switch (type)
        {
            case "Customer":
            {
                var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
                if (c is null) return new(false, "", null, null);
                if (isSales && c.OwnerId != me) return new(false, "", null, null);
                return new(true, c.CustomerName, c.CustomerId, null);
            }
            case "Lead":
            {
                var l = await db.Leads.AsNoTracking().FirstOrDefaultAsync(x => x.LeadId == id);
                if (l is null) return new(false, "", null, null);
                if (isSales && l.OwnerId != me) return new(false, "", null, null);
                return new(true, l.LeadName, null, l.LeadId);
            }
            case "Opportunity":
            {
                var o = await db.Opportunities.AsNoTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.OpportunityId == id);
                if (o is null) return new(false, "", null, null);
                if (isSales && o.OwnerId != me) return new(false, "", null, null);
                return new(true, o.OpportunityName, o.CustomerId, o.LeadId);
            }
            default:
                return new(false, "", null, null);
        }
    }
}
