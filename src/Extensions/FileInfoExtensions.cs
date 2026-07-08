using System.IO;

internal static class FileInfoExtensions
{
    public static string GetIcon(this FileInfo @this)
        => @this.Extension.ToLowerInvariant() switch
        {
            ".txt" => ":memo:",
            ".md" => ":memo:",
            ".json" => ":card_index_dividers:",
            ".xml" => ":scroll:",
            ".csv" => ":bar_chart:",
            ".jpg" or ".jpeg" or ".png" or ".gif" => ":framed_picture:",
            ".zip" or ".rar" or ".7z" => ":card_file_box:",
            _ => ":page_facing_up:"
        };
}