using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Renamer.Services;

public static class NameTemplate
{
    private static readonly char[] InvalidChars = ['/', ':', '\0'];
    private static readonly Regex WhenPattern = new(@"\{when:\s*([^}]+)\}");
    private static readonly Regex EmptyBrackets = new(@"\(\s*\)|\[\s*\]");

    public static string RenderArticle(string template, string author, string year, string title)
    {
        var result = template
            .Replace("{name}", author)
            .Replace("{year}", year)
            .Replace("{title}", title);
        return Sanitize(result);
    }

    public static string RenderPoster(string template, string eventTitle, string year, string month, string day)
    {
        var result = WhenPattern.Replace(template, m =>
            FormatDate(m.Groups[1].Value.Trim(), year, month, day));
        result = result.Replace("{title}", eventTitle);
        return Sanitize(result);
    }

    private static string FormatDate(string format, string year, string month, string day)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < format.Length)
        {
            if (i + 3 < format.Length && format[i..(i + 4)] == "YYYY")
            { sb.Append(year.PadLeft(4, '0')); i += 4; }
            else if (i + 1 < format.Length && format[i..(i + 2)] == "YY")
            { sb.Append(year.Length >= 2 ? year[^2..] : year); i += 2; }
            else if (i + 1 < format.Length && format[i..(i + 2)] == "MM")
            { sb.Append(month.PadLeft(2, '0')); i += 2; }
            else if (format[i] == 'M')
            { sb.Append(month.TrimStart('0')); i++; }
            else if (i + 1 < format.Length && format[i..(i + 2)] == "DD")
            { sb.Append(day.PadLeft(2, '0')); i += 2; }
            else if (format[i] == 'D')
            { sb.Append(day.TrimStart('0')); i++; }
            else
            { sb.Append(format[i]); i++; }
        }
        return sb.ToString();
    }

    private static string Sanitize(string name)
    {
        foreach (var c in InvalidChars) name = name.Replace(c, '-');
        name = name.Replace('<', '〈').Replace('>', '〉');
        string prev;
        do { prev = name; name = EmptyBrackets.Replace(name, ""); }
        while (name != prev);
        return name.Trim();
    }

    public static string MakeUnique(string folder, string baseName, string ext)
    {
        var path = Path.Combine(folder, baseName + ext);
        if (!File.Exists(path)) return path;
        int n = 2;
        while (true)
        {
            path = Path.Combine(folder, $"{baseName} ({n}){ext}");
            if (!File.Exists(path)) return path;
            n++;
        }
    }
}
