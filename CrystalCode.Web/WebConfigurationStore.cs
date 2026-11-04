using System.Text.Json;

using CrystalCode.Web.Searching;

namespace CrystalCode.Web;

internal sealed class WebConfigurationStore
{
    private const string HomeVariable = "CRYSTAL_HOME";
    private const string ConfigurationFileName = "web.json";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _path;

    public WebConfigurationStore()
        : this(ResolvePath())
    {
    }

    internal WebConfigurationStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public WebConfiguration Read()
    {
        if (!File.Exists(_path))
        {
            return WebConfiguration.Default;
        }

        try
        {
            using var stream = File.OpenRead(_path);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            var configuration = JsonSerializer.Deserialize<WebConfiguration>(stream, options);
            return configuration is { Provider.Length: > 0, ApiKeys: not null }
                ? configuration
                : WebConfiguration.Default;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The Web extension configuration file is invalid or cannot be read.");
        }
    }

    public void Write(WebConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!SearchProviders.TryParse(configuration.Provider, out var provider))
        {
            throw new ArgumentException("The Web search provider is not supported.", nameof(configuration));
        }

        var normalizedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, key) in configuration.ApiKeys)
        {
            if (!SearchProviders.TryParse(name, out var keyProvider)
                || string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("The Web API key configuration is invalid.", nameof(configuration));
            }

            normalizedKeys[SearchProviders.Get(keyProvider).Name] = key.Trim();
        }

        configuration = new WebConfiguration(SearchProviders.Get(provider).Name, normalizedKeys);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var fileOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.WriteThrough
            };
            if (!OperatingSystem.IsWindows())
            {
                fileOptions.UnixCreateMode = OwnerOnly;
            }

            using (var stream = new FileStream(temporaryPath, fileOptions))
            {
                JsonSerializer.Serialize(stream, configuration, options);
                stream.Flush(true);
            }

            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ResolvePath()
    {
        var home = Environment.GetEnvironmentVariable(HomeVariable);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                throw new InvalidOperationException("The user profile directory is not available.");
            }

            home = Path.Combine(home, ".crystal");
        }

        return Path.Combine(home, ConfigurationFileName);
    }
}
