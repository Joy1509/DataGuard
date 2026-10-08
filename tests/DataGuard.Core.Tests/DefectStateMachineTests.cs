using DataGuard.Core.Enums;
using DataGuard.Core.Services;

namespace DataGuard.Core.Tests;

public class DefectStateMachineTests
{
    [Theory]
    [InlineData(DefectStatus.Open, DefectStatus.Acknowledged, true)]
    [InlineData(DefectStatus.Open, DefectStatus.Resolved, true)]
    [InlineData(DefectStatus.Open, DefectStatus.WontFix, true)]
    [InlineData(DefectStatus.Open, DefectStatus.Reopened, false)]
    [InlineData(DefectStatus.Resolved, DefectStatus.Reopened, true)]
    [InlineData(DefectStatus.Resolved, DefectStatus.Acknowledged, false)]
    [InlineData(DefectStatus.Reopened, DefectStatus.Resolved, true)]
    [InlineData(DefectStatus.WontFix, DefectStatus.Open, true)]
    public void CanTransition_ShouldValidateStateMatrix(DefectStatus current, DefectStatus target, bool expected)
    {
        var result = DefectStateMachine.CanTransition(current, target);
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidateTransition_WhenInvalid_ShouldThrow()
    {
        var act = () => DefectStateMachine.ValidateTransition(DefectStatus.Resolved, DefectStatus.Open);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Invalid defect status transition*");
    }

    [Theory]
    [InlineData(RuleStatus.Failed, null, ResultClassification.New)]
    [InlineData(RuleStatus.Failed, RuleStatus.Passed, ResultClassification.New)]
    [InlineData(RuleStatus.Failed, RuleStatus.Failed, ResultClassification.Recurring)]
    [InlineData(RuleStatus.Passed, RuleStatus.Failed, ResultClassification.Fixed)]
    [InlineData(RuleStatus.Passed, RuleStatus.Passed, ResultClassification.SteadyPass)]
    [InlineData(RuleStatus.Passed, null, ResultClassification.SteadyPass)]
    public void ClassificationEngine_ShouldClassifyRunOverRun(RuleStatus current, RuleStatus? previous, ResultClassification expected)
    {
        var result = ClassificationEngine.Classify(current, previous);
        result.Should().Be(expected);
    }
}
