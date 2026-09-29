using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Tests;

// Lines below are verbatim yt-dlp 2026.08.19 output using DownloadArgs' progress templates.
public class OutputLineParserTests
{
    [Fact]
    public void Parses_download_progress_with_speed_and_eta()
    {
        var e = OutputLineParser.Parse("YTDL|downloading|130048|252182|NA|16059517.04793283|3");

        var p = Assert.IsType<DownloadProgress>(e);
        Assert.Equal(130048, p.DownloadedBytes);
        Assert.Equal(252182, p.TotalBytes);
        Assert.Equal(130048d / 252182, p.Fraction!.Value, 6);
        Assert.Equal(16059517.04793283, p.BytesPerSecond!.Value, 3);
        Assert.Equal(TimeSpan.FromSeconds(3), p.Eta);
    }

    [Fact]
    public void Reports_the_stream_file_being_written_even_with_pipes_in_the_path()
    {
        var p = Assert.IsType<DownloadProgress>(OutputLineParser.Parse(
            @"YTDL|downloading|1024|433081|NA|NA|NA|C:\out\A | B [jNQXAC9IVRw].f133.mp4"));

        Assert.Equal(@"C:\out\A | B [jNQXAC9IVRw].f133.mp4", p.StreamPath);
    }

    [Fact]
    public void Finished_stream_carries_its_path()
    {
        var f = Assert.IsType<StreamFinished>(OutputLineParser.Parse(
            @"YTDL|finished|309288|309288|NA|9047116.185120095|NA|C:\out\Me at the zoo [jNQXAC9IVRw].f140.m4a"));

        Assert.Equal(@"C:\out\Me at the zoo [jNQXAC9IVRw].f140.m4a", f.StreamPath);
    }

    [Fact]
    public void Treats_NA_fields_as_unknown()
    {
        var p = Assert.IsType<DownloadProgress>(OutputLineParser.Parse("YTDL|downloading|1024|NA|NA|NA|NA"));

        Assert.Equal(1024, p.DownloadedBytes);
        Assert.Null(p.Fraction);
        Assert.Null(p.BytesPerSecond);
        Assert.Null(p.Eta);
    }

    [Fact]
    public void Reports_a_finished_stream()
    {
        Assert.IsType<StreamFinished>(OutputLineParser.Parse("YTDL|finished|252182|252182|NA|10202835.64510466|NA"));
    }

    [Theory]
    [InlineData("YTPP|started|Merger", "Merger")]
    [InlineData("YTPP|started|ExtractAudio", "ExtractAudio")]
    public void Reports_post_processing_step_when_it_starts(string line, string step)
    {
        var e = Assert.IsType<PostProcessing>(OutputLineParser.Parse(line));
        Assert.Equal(step, e.Step);
    }

    [Fact]
    public void Ignores_post_processing_finished_lines()
    {
        Assert.Null(OutputLineParser.Parse("YTPP|finished|Merger"));
    }

    [Fact]
    public void Reports_final_file_path_including_pipes_and_brackets()
    {
        var e = OutputLineParser.Parse(@"YTFILE|C:\out\A | B [jNQXAC9IVRw].mp4");

        Assert.Equal(@"C:\out\A | B [jNQXAC9IVRw].mp4", Assert.IsType<FileCompleted>(e).Path);
    }

    [Fact]
    public void Reports_errors_without_the_prefix()
    {
        var e = OutputLineParser.Parse("ERROR: [youtube] abc: Video unavailable");

        Assert.Equal("[youtube] abc: Video unavailable", Assert.IsType<DownloadError>(e).Message);
    }

    [Fact]
    public void Ignores_unrelated_lines()
    {
        Assert.Null(OutputLineParser.Parse("[youtube] Extracting URL: https://www.youtube.com/watch?v=x"));
    }
}
