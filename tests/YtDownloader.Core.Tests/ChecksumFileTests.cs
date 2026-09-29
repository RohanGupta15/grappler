using YtDownloader.Core.Engine;

namespace YtDownloader.Core.Tests;

public class ChecksumFileTests
{
    [Fact]
    public void Finds_hash_for_named_file_in_sha256sums_listing()
    {
        const string listing = """
            66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a  yt-dlp.exe
            05b438997bafc3affdfda9d041353c9d73e04dc842207254b655b0887c4445b0  yt-dlp_arm64.exe
            """;

        var hash = ChecksumFile.FindSha256(listing, "yt-dlp.exe");

        Assert.Equal("66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a", hash);
    }

    [Fact]
    public void Finds_hash_in_deno_powershell_style_file()
    {
        const string denoFile = """

            Algorithm : SHA256
            Hash      : A0C3101B4158D1DFB7D6A78A7BF0F3DE80C96BB423C152BEEC8BEB22786F2238
            Path      : C:\a\deno\deno\target\release\deno-x86_64-pc-windows-msvc.zip
            """;

        var hash = ChecksumFile.FindSha256(denoFile, "deno-x86_64-pc-windows-msvc.zip");

        Assert.Equal("a0c3101b4158d1dfb7d6a78a7bf0f3de80c96bb423c152beec8beb22786f2238", hash);
    }

    [Fact]
    public void Verifies_file_contents_against_expected_hash_case_insensitively()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "abc");
        try
        {
            Assert.True(ChecksumFile.Verify(path, "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD"));
            Assert.False(ChecksumFile.Verify(path, "0000000000000000000000000000000000000000000000000000000000000000"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Returns_null_when_file_is_not_listed()
    {
        Assert.Null(ChecksumFile.FindSha256("abc123  other.exe", "yt-dlp.exe"));
    }
}
