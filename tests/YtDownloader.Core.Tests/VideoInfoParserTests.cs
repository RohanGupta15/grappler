using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Tests;

public class VideoInfoParserTests
{
    // Trimmed from real `yt-dlp -J` output for https://www.youtube.com/watch?v=jNQXAC9IVRw
    private static readonly string ZooJson = File.ReadAllText(Path.Combine("Fixtures", "me-at-the-zoo.json"));

    [Fact]
    public void Reads_title_channel_duration_and_thumbnail()
    {
        var info = VideoInfoParser.Parse(ZooJson);

        Assert.Equal("jNQXAC9IVRw", info.Id);
        Assert.Equal("Me at the zoo", info.Title);
        Assert.Equal("jawed", info.Channel);
        Assert.Equal(TimeSpan.FromSeconds(19), info.Duration);
        Assert.StartsWith("https://i.ytimg.com/vi/jNQXAC9IVRw/", info.ThumbnailUrl);
    }

    [Fact]
    public void Lists_distinct_video_heights_highest_first()
    {
        var info = VideoInfoParser.Parse(ZooJson);

        Assert.Equal([240, 144], info.AvailableHeights);
    }

    [Fact]
    public void Falls_back_to_uploader_and_tolerates_missing_fields()
    {
        var info = VideoInfoParser.Parse("""{"id":"x","title":"T","uploader":"Someone","formats":[]}""");

        Assert.Equal("Someone", info.Channel);
        Assert.Null(info.Duration);
        Assert.Null(info.ThumbnailUrl);
        Assert.Empty(info.AvailableHeights);
    }
}
