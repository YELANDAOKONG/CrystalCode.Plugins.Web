using CrystalCode.Plugins.Commands.Interfaces;
using CrystalCode.Plugins.Environment;
using CrystalCode.Plugins.Environment.Interfaces;

using CrystalCode.Web;
using CrystalCode.Web.Searching;

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
    public void UsesHostResolvedHomeDirectoryForConfiguration()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new WebConfigurationStore(Path.Combine(temporaryDirectory, "fallback", "web.json"));
        var plugin = new WebPlugin(store);
        var command = Assert.Single(plugin.Contribute().Commands);
        plugin.Attach(new TestPluginEnvironment(temporaryDirectory));

        command.Execute("provider exa", new TestPluginOutput());

        Assert.True(File.Exists(Path.Combine(temporaryDirectory, "web.json")));
        Assert.False(File.Exists(Path.Combine(temporaryDirectory, "fallback", "web.json")));
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

    [Fact]
    public void ToolsDeclareReadOnlyNetworkApprovalAndTheClassifierMatches()
    {
        var store = new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));
        var contribution = new WebPlugin(store).Contribute();
        var classifier = Assert.Single(contribution.Classifiers);

        Assert.Equal(["websearch", "webfetch"], contribution.Tools.Select(static tool => tool.Name));
        foreach (var tool in contribution.Tools)
        {
            Assert.True(tool.AutoApprovesReadOnlyNetwork);
            var call = new Crystal.Tools.ToolCall("1", tool.Name, "{}", Crystal.Tools.ToolInputKind.Json);
            Assert.True(classifier.TryClassify(call, Path.GetTempPath(), out var classification));
            Assert.Equal(CrystalCode.Plugins.Approvals.PluginRisk.Read, classification!.Risk);
            Assert.Equal(CrystalCode.Plugins.Approvals.PluginAuthority.Network, classification.Authority);
        }
    }

    [Fact]
    public void CommandOffersEveryProviderAsAnArgumentCompletion()
    {
        var store = new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));
        var command = Assert.Single(new WebPlugin(store).Contribute().Commands);

        var provider = Assert.Single(command.Arguments);

        Assert.Equal("provider", provider.Name);
        Assert.Equal(
            SearchProviders.All.Select(static definition => definition.Name),
            provider.Arguments.Select(static argument => argument.Name));
    }

    private sealed class TestPluginOutput : IPluginOutput
    {
        public List<string> Messages { get; } = [];

        public List<string> Errors { get; } = [];

        public void Write(string text) => Messages.Add(text);

        public void Fail(string text) => Errors.Add(text);
    }

    private sealed class TestPluginEnvironment(string homeDirectory) : IPluginEnvironment
    {
        public string HomeDirectory { get; } = homeDirectory;

        public IReadOnlyList<PluginPeer> Plugins => [];

        public IReadOnlyList<ExternalToolSetPeer> ToolSets => [];

        public IReadOnlyList<ExternalToolPeer> ExternalTools => [];

        public IReadOnlyList<SkillPeer> Skills => [];
    }
}
