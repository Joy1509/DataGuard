using DataGuard.Core.Enums;

namespace DataGuard.Core.Services;

/// <summary>
/// Enforces valid state machine transitions for tracked defects.
/// </summary>
public static class DefectStateMachine
{
    private static readonly Dictionary<DefectStatus, HashSet<DefectStatus>> AllowedTransitions = new()
    {
        [DefectStatus.Open] = new() { DefectStatus.Acknowledged, DefectStatus.Resolved, DefectStatus.WontFix },
        [DefectStatus.Acknowledged] = new() { DefectStatus.Open, DefectStatus.Resolved, DefectStatus.WontFix },
        [DefectStatus.Resolved] = new() { DefectStatus.Reopened },
        [DefectStatus.Reopened] = new() { DefectStatus.Acknowledged, DefectStatus.Resolved, DefectStatus.WontFix },
        [DefectStatus.WontFix] = new() { DefectStatus.Open, DefectStatus.Reopened }
    };

    public static bool CanTransition(DefectStatus current, DefectStatus target)
    {
        if (current == target) return true;
        return AllowedTransitions.TryGetValue(current, out var validTargets) && validTargets.Contains(target);
    }

    public static void ValidateTransition(DefectStatus current, DefectStatus target)
    {
        if (!CanTransition(current, target))
        {
            throw new InvalidOperationException($"Invalid defect status transition from '{current}' to '{target}'.");
        }
    }
}
