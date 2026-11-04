using CrystalCode.Web;

namespace CrystalCode.Web.Tests;

public sealed class WebConfigurationStoreTests
{
    [Fact]
    public void ReadsDefaultsWhenFileIsMissing()
    {
        var store = new WebConfigurationStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));

        var configuration = store.Read();

        Assert.Equal("parallel", configuration.Provider);
        Assert.Empty(configuration.ApiKeys);
    }

    [Fact]
    public void WritesAndReadsProviderAndKeys()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "web.json");
        var store = new WebConfigurationStore(path);
        store.Write(new WebConfiguration("tavily", new Dictionary<string, string> { ["tavily"] = "test-key" }));

        var configuration = store.Read();

        Assert.Equal("tavily", configuration.Provider);
        Assert.Equal("test-key", configuration.ApiKeys["tavily"]);
        Assert.Contains("\"provider\"", File.ReadAllText(path), StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(path));
        }
    }
}
