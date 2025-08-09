using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform;
using Lucide.Avalonia;

namespace ProseFlow.UI.Converters;

/// <summary>
/// Converts a string into a renderable StreamGeometry icon by intelligently detecting the input format.
/// Supported formats:
/// 1. A LucideIconKind enum name (e.g., "Pen", "Save").
/// 2. Full SVG XML content (e.g., "<svg>...</svg>").
/// 3. A URI to an asset (e.g., "avares://...") or a URL ("http://...").
/// 4. An absolute filesystem path to an SVG file.
/// 5. Raw SVG path data (e.g., "M12 2L2 22h20z").
/// If the input is invalid or the format is unrecognized, a default icon is returned.
/// </summary>
public partial class StringToIconConverter : IValueConverter
{
    private const string SvgStartTag = "<svg";
    private const string SvgEndTag = "</svg>";
    private const string DefaultIconUri = "avares://ProseFlow.UI/Assets/Icons/default.svg";

    // Regex to extract the 'd' attribute from one or more <path> tags.
    private static readonly Regex SvgPathDataRegex = SvgPathRegex();

    // Cache the default icon to avoid reloading and reparsing it repeatedly.
    private static StreamGeometry? _defaultIcon;

    // Cache for the reflection-based method call
    private static MethodInfo? _createGeometryStringMethod;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inputString = value as string;

        // Also handle direct binding of LucideIconKind for convenience (e.g., in previews)
        if (value is LucideIconKind directKind) inputString = directKind.ToString();

        if (string.IsNullOrWhiteSpace(inputString))
            return GetDefaultIcon();

        try
        {
            // 1. Check if the input is a valid LucideIconKind enum name
            if (Enum.TryParse<LucideIconKind>(inputString, true, out var kind))
            {
                var pathData = GetPathDataFromLucideKind(kind);
                return StreamGeometry.Parse(pathData);
            }

            var path = inputString.Trim();
            
            // 2. Check if the input is a valid SVG path
            return path switch
            {
                // 1. Full SVG content
                not null when path.StartsWith(SvgStartTag, StringComparison.OrdinalIgnoreCase) &&
                              path.EndsWith(SvgEndTag, StringComparison.OrdinalIgnoreCase)
                    => ParseSvgContent(path),

                // 2. URI, URL, or absolute file path
                not null when path.Contains("://") || Path.IsPathRooted(path)
                    => ParseFromFile(path),
                
                // 3. Raw SVG path data (heuristic check)
                not null when path.StartsWith('M') || path.StartsWith('m')
                    => StreamGeometry.Parse(path),

                // 4. Fallback for unrecognized formats
                _ => GetDefaultIcon()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to convert icon from string '{inputString}'. {ex.Message}");
            return GetDefaultIcon();
        }
    }
    
    /// <summary>
    /// Uses reflection to call the internal CreateGeometryString method from the Lucide.Avalonia library.
    /// This is necessary because the path data for each icon is not publicly exposed.
    /// </summary>
    private static string GetPathDataFromLucideKind(LucideIconKind kind)
    {
        try
        {
            if (_createGeometryStringMethod is null)
            {
                // Find the type and method once and cache it for performance.
                var iconToGeometryType = typeof(LucideIcon).Assembly.GetType("Lucide.Avalonia.IconToGeometry");
                if (iconToGeometryType is null) throw new InvalidOperationException("Lucide.Avalonia.IconToGeometry type not found.");

                _createGeometryStringMethod = iconToGeometryType.GetMethod("CreateGeometryString", BindingFlags.Public | BindingFlags.Static);
                if (_createGeometryStringMethod is null) throw new InvalidOperationException("CreateGeometryString method not found in IconToGeometry.");
            }
            
            // Invoke the static method with the enum value.
            return (string?)_createGeometryStringMethod.Invoke(null, [kind]) ?? string.Empty;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Reflection failed for LucideIconKind '{kind}': {ex.Message}");
            return string.Empty; // Return empty path on failure
        }
    }


    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{nameof(StringToIconConverter)} does not support ConvertBack.");
    }

    /// <summary>
    /// Parses an icon from a URI or a local filesystem path.
    /// </summary>
    private static StreamGeometry ParseFromFile(string path)
    {
        var stream = path.Contains("://")
            ? AssetLoader.Open(new Uri(path))
            : new FileStream(path, FileMode.Open, FileAccess.Read);

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd().Trim();

            // The loaded file could contain full SVG XML or just raw path data.
            return content.StartsWith(SvgStartTag, StringComparison.OrdinalIgnoreCase)
                ? ParseSvgContent(content)
                : StreamGeometry.Parse(content);
        }
    }

    /// <summary>
    /// Extracts path data from a full SVG XML string that may contain multiple <path/> elements.
    /// </summary>
    private static StreamGeometry ParseSvgContent(string svgContent)
    {
        var matches = SvgPathDataRegex.Matches(svgContent);

        if (matches.Count == 0)
            throw new FormatException("SVG content does not contain any <path> elements with a 'd' attribute.");

        // StreamGeometry can parse multiple paths if they are combined into a single string.
        var combinedPaths = new StringBuilder();
        foreach (Match match in matches)
            if (match.Success) combinedPaths.Append(match.Groups[1].Value).Append(' ');

        return StreamGeometry.Parse(combinedPaths.ToString());
    }

    /// <summary>
    /// Lazily loads and caches the default icon from application assets.
    /// </summary>
    private static StreamGeometry GetDefaultIcon()
    {
        return _defaultIcon ??= ParseFromFile(DefaultIconUri);
    }

    [GeneratedRegex("""<path[^>]*d="([^"]+)"[^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex SvgPathRegex();
}