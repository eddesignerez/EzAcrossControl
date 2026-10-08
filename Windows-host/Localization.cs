using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WindowsHost;

public record LanguageOption(string Code, string Label)
{
    public override string ToString() => Label;
}

public static class Localization
{
    private static readonly JsonDocument Catalog = JsonDocument.Parse(
        typeof(Localization).Assembly.GetManifestResourceStream("WindowsHost.Localization.catalog.json")!);
    private static readonly Dictionary<TextBlock, string> Statuses = new();
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EZ Across Control", "language.txt");
    public static string Preference { get; private set; } = "system";
    public static string Locale { get; private set; } = "en";
    public static IEnumerable<LanguageOption> Languages => Catalog.RootElement.GetProperty("languages").EnumerateArray()
        .Select(x => new LanguageOption(x.GetProperty("code").GetString()!, x.GetProperty("label").GetString()!));

    public static string Resolve(string code)
    {
        if (code == "system") code = CultureInfo.CurrentUICulture.Name;
        if (code.StartsWith("pt", StringComparison.OrdinalIgnoreCase)) return "pt-BR";
        if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
        return Languages.Select(x => x.Code).FirstOrDefault(x => code.Equals(x, StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(x + "-", StringComparison.OrdinalIgnoreCase)) ?? "en";
    }

    public static void Initialize()
    {
        try { if (File.Exists(PreferencePath)) Preference = File.ReadAllText(PreferencePath).Trim(); }
        catch (IOException) { }
        Apply(Preference);
    }

    public static string T(string source)
    {
        var strings = Catalog.RootElement.GetProperty("translations").GetProperty(Locale);
        var suffix = source.EndsWith("...") ? "..." : "";
        var key = suffix.Length > 0 ? source[..^3] : source;
        foreach (var entry in strings.EnumerateObject())
            if (entry.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) return entry.Value.GetString()! + suffix;
        return source;
    }

    public static void SetStatus(TextBlock text, string source)
    {
        Statuses[text] = source;
        text.Text = T(source);
    }

    public static void Apply(string preference)
    {
        Preference = preference == "system" || Languages.Any(x => x.Code == preference) ? preference : "system";
        Locale = Resolve(Preference);
        System.Windows.Application.Current.Resources["FieldLabelWidth"] = new GridLength(155);
        foreach (var entry in Catalog.RootElement.GetProperty("translations").GetProperty(Locale).EnumerateObject())
            System.Windows.Application.Current.Resources["Ui." + entry.Name] = entry.Value.GetString()!;
        foreach (var entry in Statuses) entry.Key.Text = T(entry.Value);
    }

    public static void Save(string preference)
    {
        Apply(preference);
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        File.WriteAllText(PreferencePath, Preference);
    }
}
