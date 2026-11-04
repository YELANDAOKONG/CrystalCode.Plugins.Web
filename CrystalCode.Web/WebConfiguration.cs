namespace CrystalCode.Web;

internal sealed record WebConfiguration(
    string Provider,
    IReadOnlyDictionary<string, string> ApiKeys)
{
    public static WebConfiguration Default { get; } = new(
        "parallel",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
