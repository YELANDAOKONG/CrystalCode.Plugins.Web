using CrystalCode.Plugins.Commands.Interfaces;

using CrystalCode.Web;

namespace CrystalCode.Web.Tests;

public sealed class WebPluginTests
{
    [Fact]
    public void ProviderCommandPersistsTheSelectedProvider()
    {
        var store = new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));
        var plugin = new WebPlugin(store);
        var command = Assert.Single(plugin.Contribute().Commands);
        var output = new TestPluginOutput();

        command.Execute("provider tavily", output);

        Assert.Equal("tavily", store.Read().Provider);
        Assert.Contains("Web search provider set to tavily.", output.Messages);
    }

    [Fact]
    public void ProviderCommandRejectsUnknownProviders()
    {
        var store = new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));
        var command = Assert.Single(new WebPlugin(store).Contribute().Commands);
        var output = new TestPluginOutput();

        command.Execute("provider unknown", output);

        Assert.Equal("parallel", store.Read().Provider);
        Assert.Contains("Provider must be", output.Errors.Single(), StringComparison.Ordinal);
    }

    private sealed class TestPluginOutput : IPluginOutput
    {
        public List<string> Messages { get; } = [];

        public List<string> Errors { get; } = [];

        public void Write(string text) => Messages.Add(text);

        public void Fail(string text) => Errors.Add(text);
    }
}
