using System.Net;

using Crystal.Tools;

using CrystalCode.Web.Searching;
using CrystalCode.Web.Tools;

namespace CrystalCode.Web.Tests.Tools;

public sealed class WebSearchToolTests
{
    [Fact]
    public void ExposesTheWebsearchDefinition()
    {
        var tool = new WebSearchTool();

        var input = Assert.IsType<JsonToolInput>(tool.Definition.Input);
        Assert.Equal("websearch", tool.Definition.Name);
        Assert.NotNull(tool.Definition.Description);
        Assert.Equal(ToolInputKind.Json, input.Kind);
        Assert.False(input.Schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "query",
            input.Schema.GetProperty("required").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task RejectsInvalidArguments()
    {
        using var http = new HttpClient(new FakeHttpMessageHandler());
        var tool = CreateTool(http, TestEnvironment.Empty);

        var output = await tool.InvokeAsync(
            new ToolCall("1", "websearch", "{}", ToolInputKind.Json));

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Equal("Argument 'query' must be a non-empty string.", output.Text);
    }

    [Fact]
    public async Task ReturnsSearchText()
    {
        var handler = new FakeHttpMessageHandler().Respond(
            """
            {"result":{"content":[{"type":"text","text":"found it"}]}}
            """);
        using var http = new HttpClient(handler);
        var tool = CreateTool(http, TestEnvironment.Empty);

        var output = await tool.InvokeAsync(
            new ToolCall("1", "websearch", """{"query":"crystal"}""", ToolInputKind.Json));

        Assert.Equal(ToolResultStatus.Success, output.Status);
        Assert.Equal("found it", output.Text);
    }

    [Fact]
    public async Task ReturnsAFailureForAnHttpError()
    {
        var handler = new FakeHttpMessageHandler().Respond(
            _ => TestResponses.Status(HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);
        var tool = CreateTool(http, TestEnvironment.Empty);

        var output = await tool.InvokeAsync(
            new ToolCall("1", "websearch", """{"query":"crystal"}""", ToolInputKind.Json));

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Equal("Parallel search failed with HTTP status 500.", output.Text);
    }

    [Fact]
    public async Task ReturnsAFailureWhenTheProviderKeyIsMissing()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = new HttpClient(handler);
        var environment = TestEnvironment.Empty;
        var configuration = new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json"));
        configuration.Write(new WebConfiguration("tavily", new Dictionary<string, string>()));
        var tool = new WebSearchTool(http, environment, configuration);

        var output = await tool.InvokeAsync(
            new ToolCall("1", "websearch", """{"query":"crystal"}""", ToolInputKind.Json));

        Assert.Equal(ToolResultStatus.Failure, output.Status);
        Assert.Equal("An API key is required for Tavily search. Set TAVILY_API_KEY or add the key to web.json.", output.Text);
        Assert.Empty(handler.Requests);
    }

    private static WebSearchTool CreateTool(HttpClient http, Func<string, string?> environment) =>
        new(http, environment, new WebConfigurationStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "web.json")));
}
