using MiniPdm.Domain;

namespace MiniPdm.Tests.Domain;

/// <summary>
/// Проверка правил жизненного цикла версий.
/// </summary>
public sealed class VersionStateRulesTests
{
    [Theory]
    [InlineData(VersionState.InWork, VersionState.Approved)]
    [InlineData(VersionState.InWork, VersionState.Annulled)]
    [InlineData(VersionState.Approved, VersionState.Annulled)]
    public void AllowedTransitions_AreAccepted(VersionState from, VersionState to)
    {
        Assert.True(VersionStateRules.CanTransition(from, to));
        VersionStateRules.EnsureCanTransition(from, to);
    }

    [Theory]
    [InlineData(VersionState.Approved, VersionState.InWork)]
    [InlineData(VersionState.Annulled, VersionState.InWork)]
    [InlineData(VersionState.Annulled, VersionState.Approved)]
    [InlineData(VersionState.InWork, VersionState.InWork)]
    [InlineData(VersionState.Approved, VersionState.Approved)]
    public void ForbiddenTransitions_Throw(VersionState from, VersionState to)
    {
        Assert.False(VersionStateRules.CanTransition(from, to));
        Assert.Throws<InvalidStateTransitionException>(() => VersionStateRules.EnsureCanTransition(from, to));
    }

    [Fact]
    public void AnnulledVersion_DoesNotParticipateInCalculations()
    {
        Assert.False(VersionStateRules.ParticipatesInCalculations(VersionState.Annulled));
        Assert.True(VersionStateRules.ParticipatesInCalculations(VersionState.InWork));
        Assert.True(VersionStateRules.ParticipatesInCalculations(VersionState.Approved));
    }

    [Fact]
    public void OnlyInWorkVersion_IsMutable()
    {
        Assert.True(VersionStateRules.IsMutable(VersionState.InWork));
        Assert.False(VersionStateRules.IsMutable(VersionState.Approved));
        Assert.False(VersionStateRules.IsMutable(VersionState.Annulled));
    }

    [Fact]
    public void AllowedTargets_MatchRules()
    {
        Assert.Equal(
            new[] { VersionState.Approved, VersionState.Annulled },
            VersionStateRules.AllowedTargets(VersionState.InWork).Order().ToArray());

        Assert.Equal(
            new[] { VersionState.Annulled },
            VersionStateRules.AllowedTargets(VersionState.Approved));

        Assert.Empty(VersionStateRules.AllowedTargets(VersionState.Annulled));
    }
}