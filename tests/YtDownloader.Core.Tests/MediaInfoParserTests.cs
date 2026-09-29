using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Tests;

public class MediaInfoParserTests
{
    // Trimmed from real `yt-dlp -J --flat-playlist` output for
    // https://www.youtube.com/playlist?list=PLav47HAVZMjk8nQDr4iPHA8x4qnrcqRlZ (10 videos; first 4 kept).
    private static readonly string PlaylistJson = File.ReadAllText(Path.Combine("Fixtures", "blender-tutorials-playlist.json"));
    private static readonly string VideoJson = File.ReadAllText(Path.Combine("Fixtures", "me-at-the-zoo.json"));

    [Fact]
    public void Recognises_a_single_video()
    {
        var info = Assert.IsType<VideoInfo>(MediaInfoParser.Parse(VideoJson));
        Assert.Equal("Me at the zoo", info.Title);
    }

    [Fact]
    public void Reads_playlist_title_channel_and_total_count()
    {
        var p = Assert.IsType<PlaylistInfo>(MediaInfoParser.Parse(PlaylistJson));

        Assert.Equal("PLav47HAVZMjk8nQDr4iPHA8x4qnrcqRlZ", p.Id);
        Assert.Equal("Tutorials", p.Title);
        Assert.Equal("Blender Studio", p.Channel);
        Assert.Equal(10, p.TotalCount);
    }

    [Fact]
    public void Reads_each_entry_with_url_duration_and_thumbnail()
    {
        var p = Assert.IsType<PlaylistInfo>(MediaInfoParser.Parse(PlaylistJson));

        Assert.Equal(4, p.Entries.Count);
        var second = p.Entries[1];
        Assert.Equal("Ds_RYS0-chs", second.Id);
        Assert.Equal("Interactive Node Tools in Blender 4.2", second.Title);
        Assert.Equal("https://www.youtube.com/watch?v=Ds_RYS0-chs", second.Url);
        Assert.Equal(TimeSpan.FromSeconds(794), second.Duration);
        Assert.StartsWith("https://i.ytimg.com/vi/mBu_ZMs3lR8/", p.Entries[0].ThumbnailUrl);
    }

    [Fact]
    public void Entry_without_duration_or_thumbnails_is_still_listed()
    {
        var p = Assert.IsType<PlaylistInfo>(MediaInfoParser.Parse(
            """{"_type":"playlist","id":"P","title":"T","entries":[{"_type":"url","id":"a","url":"https://www.youtube.com/shorts/a","title":"Short"}]}"""));

        var e = Assert.Single(p.Entries);
        Assert.Null(e.Duration);
        Assert.Null(e.ThumbnailUrl);
        Assert.Equal(1, p.TotalCount);
    }
}
