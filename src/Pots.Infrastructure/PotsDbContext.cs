using Microsoft.EntityFrameworkCore;
using Pots.Domain.Entities;

namespace Pots.Infrastructure;

public sealed class PotsDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientGrant> PatientGrants => Set<PatientGrant>();
    public DbSet<GrantUpgradeRequest> GrantUpgradeRequests => Set<GrantUpgradeRequest>();
    public DbSet<CaregiverNote> CaregiverNotes => Set<CaregiverNote>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    public DbSet<DailyStatusEntry> DailyStatusEntries => Set<DailyStatusEntry>();
    public DbSet<SymptomLog> SymptomLogs => Set<SymptomLog>();
    public DbSet<VitalSignLog> VitalSignLogs => Set<VitalSignLog>();
    public DbSet<PreventiveActionLog> PreventiveActionLogs => Set<PreventiveActionLog>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<PatientTargets> PatientTargets => Set<PatientTargets>();

    public PotsDbContext(DbContextOptions<PotsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PotsDbContext).Assembly);
    }

    // Canonical SaveChanges entry points: the parameterless overloads at the
    // base class delegate to these, so all paths flow through the audit guard.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceAuditAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceAuditAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnforceAuditAppendOnly()
    {
        var tampered = ChangeTracker.Entries<AuditLogEntry>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
            throw new InvalidOperationException(
                "Audit log is append-only; UPDATE and DELETE are not permitted.");
    }
}
