namespace YtDownloader.Core.Progress;

/// <summary>What the taskbar button shows; mirrors the Windows TBPF_* states the app uses.</summary>
public enum TaskbarState { None, Indeterminate, Normal, Paused }

/// <summary>Running: downloading or post-processing. Queued: started but waiting for a free slot.</summary>
public enum ItemPhase { Queued, Running, Paused }

/// <param name="Fraction">0 to 1, or null while the size isn't known yet.</param>
public readonly record struct ItemProgress(ItemPhase Phase, double? Fraction);

/// <summary>One progress value for the whole queue, for the taskbar button.</summary>
public readonly record struct OverallProgress(TaskbarState State, double Fraction)
{
    public static OverallProgress From(IEnumerable<ItemProgress> items)
    {
        var list = items.ToList();

        // Queued downloads count as not started, so the bar reflects everything the user started.
        var inProgress = list.Where(i => i.Phase != ItemPhase.Paused).ToList();
        if (inProgress.Count > 0)
        {
            var anyKnown = inProgress.Any(i => i.Phase == ItemPhase.Running && i.Fraction is not null);
            return anyKnown
                ? new(TaskbarState.Normal, inProgress.Average(i => i.Fraction ?? 0))
                : new(TaskbarState.Indeterminate, 0);
        }

        var paused = list.Where(i => i.Phase == ItemPhase.Paused).ToList();
        return paused.Count > 0
            ? new(TaskbarState.Paused, paused.Average(i => i.Fraction ?? 0))
            : new(TaskbarState.None, 0);
    }
}
