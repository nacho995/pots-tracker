using Pots.Domain;
using Pots.Domain.Entities;
using Xunit;

namespace Pots.Domain.Tests;

// Safety-critical: when salt target is NOT enabled, the patient must not be
// able to send a "salt target reached" flag. Reinforces CLAUDE.md §2.
public sealed class PreventiveActionLogTests
{
    private static readonly Guid Patient = Guid.NewGuid();
    private static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public void Create_RejectsSaltField_WhenSaltTargetDisabled()
    {
        var ex = Assert.Throws<DomainException>(() =>
            PreventiveActionLog.Create(Patient, Day, saltTargetAllowed: false,
                new PreventiveActionData { SaltTargetReached = true }));
        Assert.Contains("salt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_AcceptsSaltField_WhenSaltTargetEnabled()
    {
        var log = PreventiveActionLog.Create(Patient, Day, saltTargetAllowed: true,
            new PreventiveActionData { SaltTargetReached = true });
        Assert.True(log.SaltTargetReached);
    }

    [Fact]
    public void Create_AcceptsNullSaltField_RegardlessOfGate()
    {
        var disabled = PreventiveActionLog.Create(Patient, Day, saltTargetAllowed: false,
            new PreventiveActionData { SaltTargetReached = null });
        var enabled = PreventiveActionLog.Create(Patient, Day, saltTargetAllowed: true,
            new PreventiveActionData { SaltTargetReached = null });
        Assert.Null(disabled.SaltTargetReached);
        Assert.Null(enabled.SaltTargetReached);
    }

    [Fact]
    public void Create_DefaultsCaffeineLevel_None()
    {
        var log = PreventiveActionLog.Create(Patient, Day, false, new PreventiveActionData());
        Assert.Equal(CaffeineLevel.None, log.CaffeineLevel);
    }
}
