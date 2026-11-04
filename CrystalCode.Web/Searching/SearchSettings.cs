namespace CrystalCode.Web.Searching;

/// <summary>
/// Resolved endpoint and credential for one search call. Required credentials
/// are validated before the request; values are never written to output.
/// </summary>
internal sealed record SearchSettings(
    SearchProviderDefinition Definition,
    Uri Endpoint,
    string? ApiKey)
{
    public static SearchSettings Resolve(
        SearchProvider provider,
        string? storedApiKey,
        Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var definition = SearchProviders.Get(provider);
        var configured = environment(definition.EndpointVariable);
        var endpointText = string.IsNullOrWhiteSpace(configured)
            ? definition.DefaultEndpoint
            : configured.Trim();
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new WebToolException(
                $"Environment variable {definition.EndpointVariable} must be an absolute http or https URL.");
        }

        var key = environment(definition.KeyVariable);
        if (string.IsNullOrWhiteSpace(key))
        {
            key = storedApiKey;
        }
        if (definition.RequiresKey && string.IsNullOrWhiteSpace(key))
        {
            throw new WebToolException(
                $"An API key is required for {definition.Title} search. Set {definition.KeyVariable} or add the key to web.json.");
        }

        return new SearchSettings(
            definition,
            endpoint,
            string.IsNullOrWhiteSpace(key) ? null : key.Trim());
    }
}
