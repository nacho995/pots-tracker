using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

// Safety-critical tests for the salt-gate from CLAUDE.md §2:
//   "Salt-loading targets only appear if the patient has explicitly marked
//    them as 'prescribed by my clinician.' Never default salt targets on."
public sealed class PatientTargetsTests
{
    [Fact]
    public void CreateDefaults_SaltTargetEnabled_IsFalseByDefault()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        Assert.False(t.SaltTargetEnabled);
        Assert.Null(t.SaltTargetMg);
        Assert.Null(t.SaltClinicianAttestation);
    }

    [Fact]
    public void EnableSaltTarget_RejectsEmptyAttestation()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        var ex = Assert.Throws<DomainException>(() => t.EnableSaltTarget(6000, ""));
        Assert.Contains("attestation", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(t.SaltTargetEnabled);
    }

    [Fact]
    public void EnableSaltTarget_RejectsWhitespaceAttestation()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        Assert.Throws<DomainException>(() => t.EnableSaltTarget(6000, "   "));
        Assert.False(t.SaltTargetEnabled);
    }

    [Fact]
    public void EnableSaltTarget_PersistsAttestation()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        t.EnableSaltTarget(6000, "Mi cardióloga, Dra. X, en consulta del 2026-05-15");
        Assert.True(t.SaltTargetEnabled);
        Assert.Equal(6000, t.SaltTargetMg);
        Assert.Contains("Dra. X", t.SaltClinicianAttestation);
    }

    [Fact]
    public void DisableSaltTarget_ClearsAllSaltFields()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        t.EnableSaltTarget(6000, "Prescrito por Dra. X");
        t.DisableSaltTarget();
        Assert.False(t.SaltTargetEnabled);
        Assert.Null(t.SaltTargetMg);
        Assert.Null(t.SaltClinicianAttestation);
    }

    [Fact]
    public void EnableSaltTarget_RejectsOutOfRangeAmount()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        Assert.Throws<DomainException>(() => t.EnableSaltTarget(-1, "valid attestation"));
        Assert.Throws<DomainException>(() => t.EnableSaltTarget(60_000, "valid attestation"));
        Assert.False(t.SaltTargetEnabled);
    }

    [Fact]
    public void Update_DoesNotAffectSaltState()
    {
        var t = PatientTargets.CreateDefaults(Guid.NewGuid());
        t.EnableSaltTarget(6000, "Prescrito por Dra. X");

        t.Update(
            hydrationTargetMl: 3000,
            compressionGoalHoursPerDay: 8,
            exercisePlanNote: "Andar 20min/día",
            sleepTargetHours: 8.0m,
            language: "es");

        // Salt fields must survive an unrelated update — they're toggled
        // through their own dedicated methods.
        Assert.True(t.SaltTargetEnabled);
        Assert.Equal(6000, t.SaltTargetMg);
    }
}
