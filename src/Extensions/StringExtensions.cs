using Spectre.Console;

internal static class StringExtensions
{
    internal static Text ToText(this string text, Style? style = null)
        => new(text, style);

    internal static Markup ToMarkdown(this string text, Style? style = null)
        => new(text, style);

    internal static string EscapeMarkup(this string text)
        => Markup.Escape(text);
}
