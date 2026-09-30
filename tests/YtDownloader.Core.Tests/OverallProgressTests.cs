using YtDownloader.Core.Progress;

namespace YtDownloader.Core.Tests;

public class OverallProgressTests
{
    private static ItemProgress Running(double? fraction) => new(ItemPhase.Running, fraction);
    private static ItemProgress Queued() => new(ItemPhase.Queued, null);
    private static ItemProgress Paused(double? fraction) => new(ItemPhase.Paused, fraction);

    [Fact]
    public void Nothing_to_show_when_the_queue_is_empty()
    {
        Assert.Equal(new OverallProgress(TaskbarState.None, 0), OverallProgress.From([]));
    }

    [Fact]
    public void One_download_shows_its_own_progress()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Normal, 0.5), OverallProgress.From([Running(0.5)]));
    }

    [Fact]
    public void Several_downloads_average_their_progress()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Normal, 0.5), OverallProgress.From([Running(1.0), Running(0.0)]));
    }

    [Fact]
    public void Queued_downloads_count_as_not_started()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Normal, 0.4), OverallProgress.From([Running(0.8), Queued()]));
    }

    [Fact]
    public void Downloads_with_no_known_size_yet_are_indeterminate()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Indeterminate, 0), OverallProgress.From([Running(null)]));
    }

    [Fact]
    public void An_unknown_size_counts_as_zero_next_to_known_ones()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Normal, 0.3), OverallProgress.From([Running(null), Running(0.6)]));
    }

    [Fact]
    public void Queued_downloads_waiting_for_a_slot_are_indeterminate()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Indeterminate, 0), OverallProgress.From([Queued(), Queued()]));
    }

    [Fact]
    public void Paused_downloads_show_as_paused_when_nothing_runs()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Paused, 0.25), OverallProgress.From([Paused(0.25)]));
    }

    [Fact]
    public void Paused_downloads_are_ignored_while_others_run()
    {
        Assert.Equal(new OverallProgress(TaskbarState.Normal, 0.5), OverallProgress.From([Running(0.5), Paused(0.9)]));
    }
}
