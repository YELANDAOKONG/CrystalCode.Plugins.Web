using CrystalCode.Plugins;
using CrystalCode.Plugins.Approvals;
using CrystalCode.Plugins.Approvals.Interfaces;
using CrystalCode.Plugins.Commands;
using CrystalCode.Plugins.Commands.Interfaces;
using CrystalCode.Plugins.Environment.Interfaces;
using CrystalCode.Plugins.Interfaces;
using CrystalCode.Plugins.Tools;
using CrystalCode.Plugins.Tools.Interfaces;

using Crystal.Tools;

using CrystalCode.Web.Searching;
using CrystalCode.Web.Tools;

namespace CrystalCode.Web;

public sealed class WebPlugin : IPlugin
{
    private readonly WebConfigurationStore _configuration;

    public WebPlugin()
        : this(new WebConfigurationStore())
    {
    }

    internal WebPlugin(WebConfigurationStore configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    public string Name => "Crystal Code Web";

    public void Attach(IPluginEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!string.IsNullOrWhiteSpace(environment.HomeDirectory))
        {
            _configuration.AttachHome(environment.HomeDirectory);
        }
    }

    public PluginContribution Contribute() => new(
        tools:
        [
            new WebPluginTool(new WebSearchTool(_configuration)),
            new WebPluginTool(new WebFetchTool())
        ],
        commands: [new WebCommand(_configuration)],
        classifiers: [new WebClassifier()]);

    private sealed class WebPluginTool(ITool tool) : IPluginTool
    {
        public string Name => tool.Definition.Name;

        public PluginToolCatalogs Catalogs => PluginToolCatalogs.PlanAndWork;

        public ITool Tool => tool;

        // The classifier below rates every call Read risk with Network
        // authority, so Home installs run without a prompt on each search.
        public bool AutoApprovesReadOnlyNetwork => true;
    }

    private sealed class WebCommand(WebConfigurationStore configuration) : IPluginCommand
    {
        public string Name => "web";

        public string Help => "/web [provider <name>] - show or change the Web search provider";

        public IReadOnlyList<PluginCommandArgument> Arguments { get; } =
        [
            new PluginCommandArgument(
                "provider",
                "Show or change the Web search provider",
                arguments: SearchProviders.All.Select(static definition => new PluginCommandArgument(
                    definition.Name,
                    definition.RequiresKey
                        ? $"{definition.Title} (API key required)"
                        : $"{definition.Title} (works without a key)"))),
        ];

        public void Execute(string argument, IPluginOutput output)
        {
            var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                var current = configuration.Read();
                output.Write($"Web search provider: {current.Provider}");
                output.Write($"Available providers: {SearchProviders.DescribeChoices()}");
                return;
            }

            if (parts.Length != 2
                || !string.Equals(parts[0], "provider", StringComparison.OrdinalIgnoreCase))
            {
                output.Fail("Usage: /web provider <name>");
                return;
            }

            if (!SearchProviders.TryParse(parts[1], out var provider))
            {
                output.Fail($"Provider must be {SearchProviders.DescribeChoices()}.");
                return;
            }

            var previous = configuration.Read();
            configuration.Write(previous with { Provider = SearchProviders.Get(provider).Name });
            output.Write($"Web search provider set to {SearchProviders.Get(provider).Name}.");
        }
    }

    private sealed class WebClassifier : IPluginClassifier
    {
        public bool TryClassify(
            ToolCall call,
            string workspaceRoot,
            out PluginClassification? classification)
        {
            _ = workspaceRoot;
            if (call.Name is "websearch" or "webfetch")
            {
                classification = new PluginClassification(
                    PluginRisk.Read,
                    PluginAuthority.Network,
                    "Web search or page fetch");
                return true;
            }

            classification = null;
            return false;
        }
    }
}
