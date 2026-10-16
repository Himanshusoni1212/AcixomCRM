using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Customer>(e =>
        {
            e.HasIndex(x => x.CustomerCode).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => new { x.OwnerId, x.Status });
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Lead>(e =>
        {
            e.HasIndex(x => x.LeadCode).IsUnique();
            e.HasIndex(x => new { x.Status, x.OwnerId });
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Opportunity>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(12,2)");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Opportunity_Amount_NonNeg", "\"Amount\" >= 0");
                t.HasCheckConstraint("CK_Opportunity_Prob", "\"Probability\" BETWEEN 0 AND 100");
            });
            e.HasIndex(x => new { x.Stage, x.OwnerId });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<FollowUp>(e =>
        {
            e.HasIndex(x => new { x.FollowUpDate, x.Status, x.AssignedTo });
        });
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EntityName, x.CreatedDate });
        });
    }

    public override int SaveChanges()
    {
        GuardAuditImmutable();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        GuardAuditImmutable();
        return base.SaveChangesAsync(ct);
    }

    private void GuardAuditImmutable()
    {
        if (AllowRetentionPurge) return; // published retention schedule only (see RetentionService)
        foreach (var e in ChangeTracker.Entries<AuditLog>())
        {
            if (e.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("AuditLog is append-only and cannot be modified or deleted.");
        }
    }

    internal bool AllowRetentionPurge { get; set; }
}
