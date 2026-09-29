using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Tests;

// Expected selectors were checked against real yt-dlp 2026.08.19 on a 4K video:
// "res:1080,vcodec:h264,ext:mp4:m4a" picks 299+140 (H.264 + AAC), "ba[ext=m4a]/ba/b" picks 140.
public class DownloadArgsTests
{
    private static readonly EnginePaths Engine = new(@"C:\e\yt-dlp.exe", @"C:\e\ffmpeg", @"C:\e\deno.exe");
    private const string Url = "https://www.youtube.com/watch?v=jNQXAC9IVRw";

    private static IReadOnlyList<string> For(Quality q) =>
        DownloadArgs.ForDownload(new DownloadRequest(Url, q, @"D:\Videos"), Engine);

    private static void AssertPair(IReadOnlyList<string> args, string flag, string value)
    {
        var i = args.ToList().IndexOf(flag);
        Assert.True(i >= 0, $"missing {flag}");
        Assert.Equal(value, args[i + 1]);
    }

    [Fact]
    public void Best_video_prefers_highest_resolution_then_h264_and_merges_to_mp4()
    {
        var args = For(Quality.BestVideo);

        AssertPair(args, "-f", "bv*+ba/b");
        AssertPair(args, "-S", "res,vcodec:h264,ext:mp4:m4a");
        AssertPair(args, "--merge-output-format", "mp4");
        Assert.DoesNotContain("-x", args);
    }

    [Fact]
    public void Capped_video_limits_resolution()
    {
        AssertPair(For(Quality.VideoUpTo(720)), "-S", "res:720,vcodec:h264,ext:mp4:m4a");
    }

    [Fact]
    public void Mp3_extracts_best_audio_at_top_quality()
    {
        var args = For(Quality.AudioMp3);

        AssertPair(args, "-f", "ba/b");
        Assert.Contains("-x", args);
        AssertPair(args, "--audio-format", "mp3");
        AssertPair(args, "--audio-quality", "0");
        Assert.DoesNotContain("--merge-output-format", args);
    }

    [Fact]
    public void M4a_prefers_native_m4a_stream_to_avoid_reencoding()
    {
        var args = For(Quality.AudioM4a);

        AssertPair(args, "-f", "ba[ext=m4a]/ba/b");
        AssertPair(args, "--audio-format", "m4a");
    }

    [Fact]
    public void Points_yt_dlp_at_bundled_engines_and_ignores_user_config()
    {
        var args = For(Quality.BestVideo);

        AssertPair(args, "--js-runtimes", @"deno:C:\e\deno.exe");
        AssertPair(args, "--ffmpeg-location", @"C:\e\ffmpeg");
        AssertPair(args, "-P", @"D:\Videos");
        Assert.Contains("--ignore-config", args);
        Assert.Contains("--no-playlist", args);
    }

    [Fact]
    public void Emits_the_tagged_lines_the_output_parser_reads()
    {
        var args = For(Quality.BestVideo);

        Assert.Contains("--newline", args);
        Assert.Contains("download:YTDL|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s", args);
        Assert.Contains("postprocess:YTPP|%(progress.status)s|%(progress.postprocessor)s", args);
        AssertPair(args, "--print", "after_move:YTFILE|%(filepath)s");
    }

    [Fact]
    public void Url_comes_last_after_end_of_options_marker()
    {
        var args = For(Quality.BestVideo);

        Assert.Equal(["--", Url], args.TakeLast(2));
    }

    [Fact]
    public void Info_request_dumps_single_json_without_playlist()
    {
        var args = DownloadArgs.ForInfo(Url, Engine);

        Assert.Contains("-J", args);
        Assert.Contains("--no-playlist", args);
        AssertPair(args, "--js-runtimes", @"deno:C:\e\deno.exe");
        Assert.Equal(["--", Url], args.TakeLast(2));
    }
}
