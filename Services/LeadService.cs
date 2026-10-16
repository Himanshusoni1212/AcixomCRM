using System.Text.Json;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public record LeadConvertResult(bool Ok, string? ErrorCode, string? Message, Customer? Customer, Opportunity? Opportunity);

public class LeadService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public LeadService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public static string? TransitionError(string from, string to)
    {
        if (!LeadTransitions.IsValidStatus(to))
            return "Lead status is required.";
        if (!LeadTransitions.CanTransition(from, to))
            return $"Invalid status transition from {from} to {to}.";
        return null;
    }

    public async Task<LeadConvertResult> ConvertAsync(int leadId, string userId, bool createOpportunity,
        string? opportunityName, decimal? amount, int? probability)
    {
        var lead = await _db.Leads.FindAsync(leadId);
        if (lead is null) return new(false, "NOT_FOUND", "Lead not found.", null, null);
        if (lead.Status == "Converted")
            return new(false, "CONFLICT", "Lead is already converted.", null, null);
        if (lead.Status != "Qualified")
            return new(false, "VALIDATION_ERROR", "Only qualified leads can be converted.", null, null);
        if (string.IsNullOrWhiteSpace(lead.Email) || string.IsNullOrWhiteSpace(lead.Phone))
            return new(false, "VALIDATION_ERROR", "Lead email and phone are required for conversion.", null, null);

        var dup = await _db.Customers.AsNoTracking().AnyAsync(c =>
            c.Email.ToLower() == lead.Email!.ToLower() || c.Phone == lead.Phone);
        if (dup)
            return new(false, "CONFLICT", "Customer with this email/phone already exists.", null, null);

        Opportunity? opp = null;
        if (createOpportunity)
        {
            if (amount.HasValue && amount.Value <= 0)
                return new(false, "VALIDATION_ERROR", "Opportunity Amount must be greater than 0.", null, null);
            if (probability.HasValue && (probability < 0 || probability > 100))
                return new(false, "VALIDATION_ERROR", "Probability must be between 0 and 100.", null, null);
        }

        var customer = new Customer
        {
            CustomerCode = await UniqueCustomerCodeAsync(),
            CustomerName = lead.LeadName,
            Email = lead.Email!,
            Phone = lead.Phone!,
            CompanyName = lead.CompanyName,
            Status = "Active",
            OwnerId = lead.OwnerId,
            CreatedBy = userId,
            CreatedDate = DateTime.UtcNow
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        if (createOpportunity)
        {
            opp = new Opportunity
            {
                OpportunityName = string.IsNullOrWhiteSpace(opportunityName) ? $"{lead.LeadName} Opportunity" : opportunityName!,
                CustomerId = customer.CustomerId,
                LeadId = lead.LeadId,
                Stage = "Qualification",
                Status = "Open",
                Amount = amount ?? lead.ExpectedValue ?? 0,
                Probability = probability ?? 0,
                OwnerId = lead.OwnerId,
                Source = lead.Source,
                CreatedDate = DateTime.UtcNow
            };
            if (opp.Amount <= 0)
            {
                // Roll back customer to keep data consistent when opp requested but amount invalid/missing.
                _db.Customers.Remove(customer);
                await _db.SaveChangesAsync();
                return new(false, "VALIDATION_ERROR", "Opportunity Amount must be greater than 0.", null, null);
            }
            _db.Opportunities.Add(opp);
            await _db.SaveChangesAsync();
        }

        var oldVal = JsonSerializer.Serialize(new { lead.LeadId, Status = "Qualified" });
        lead.Status = "Converted";
        await _db.SaveChangesAsync();
        var newVal = JsonSerializer.Serialize(new { lead.LeadId, lead.Status, CustomerId = customer.CustomerId, OpportunityId = opp?.OpportunityId });

        await _audit.LogAsync(userId, "Create", "Customer", customer.CustomerId.ToString(), "Success", $"Converted from Lead {lead.LeadId}");
        if (opp != null)
            await _audit.LogAsync(userId, "Create", "Opportunity", opp.OpportunityId.ToString(), "Success", $"Converted from Lead {lead.LeadId}");
        await _audit.LogAsync(userId, "Convert", "Lead", lead.LeadId.ToString(), "Success", $"Lead converted to Customer {customer.CustomerId}", oldVal, newVal);

        return new(true, null, null, customer, opp);
    }

    private async Task<string> UniqueCustomerCodeAsync()
    {
        var baseCode = $"CUST-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var code = baseCode;
        for (var n = 1; await _db.Customers.AnyAsync(c => c.CustomerCode == code); n++)
            code = $"{baseCode}-{n}";
        return code;
    }

    public async Task<string> UniqueLeadCodeAsync()
    {
        var baseCode = $"LEAD-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var code = baseCode;
        for (var n = 1; await _db.Leads.AnyAsync(c => c.LeadCode == code); n++)
            code = $"{baseCode}-{n}";
        return code;
    }
}
