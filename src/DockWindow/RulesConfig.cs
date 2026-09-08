using System.Diagnostics;
using System.Text.Json;

namespace DockWindow;

internal static class RulesConfig
{
    private sealed class RulesDocument
    {
        public string[]? Rules { get; set; }
    }

    public static string DefaultPath() =>
        Path.Combine(AppContext.BaseDirectory, "dockwindow.json");

    public static DockPolicy Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<RulesDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (document?.Rules is null)
                return new DockPolicy(Array.Empty<DockRule>());

            var rules = new List<DockRule>();
            foreach (var command in document.Rules)
            {
                if (DockRule.TryParse(command, out var rule) && rule is not null)
                {
                    rules.Add(rule);
                }
                else
                {
                    Trace.WriteLine($"Ignoring invalid dock rule: {command}");
                }
            }

            return new DockPolicy(rules);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Trace.WriteLine($"Could not load dock rules from '{path}': {ex.Message}");
            return new DockPolicy(Array.Empty<DockRule>());
        }
    }
}
