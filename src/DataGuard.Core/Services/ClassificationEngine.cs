using DataGuard.Core.Enums;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// Classifies rule outcomes run-over-run into New, Recurring, Fixed, or SteadyPass.
/// </summary>
public static class ClassificationEngine
{
    public static ResultClassification Classify(RuleStatus currentStatus, RuleStatus? previousStatus)
    {
        if (currentStatus == RuleStatus.Failed || currentStatus == RuleStatus.Error)
        {
            if (previousStatus == null || previousStatus == RuleStatus.Passed)
            {
                return ResultClassification.New;
            }
            if (previousStatus == RuleStatus.Failed || previousStatus == RuleStatus.Error)
            {
                return ResultClassification.Recurring;
            }
        }
        else if (currentStatus == RuleStatus.Passed)
        {
            if (previousStatus == RuleStatus.Failed || previousStatus == RuleStatus.Error)
            {
                return ResultClassification.Fixed;
            }
            return ResultClassification.SteadyPass;
        }

        return ResultClassification.Unknown;
    }
}
