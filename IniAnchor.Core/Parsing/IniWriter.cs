using System.Text;

namespace IniAnchor.Core.Parsing;

public static class IniWriter
{
    public static string WriteToString(IniDocument document)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < document.Lines.Count; i++)
        {
            sb.Append(document.Lines[i].ToRawText());

            var isLastLine = i == document.Lines.Count - 1;
            if (!isLastLine || document.HasTrailingNewLine)
                sb.Append(document.NewLine);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the document to filePath crash-safely (§3.4): the new content is written to a
    /// temp file in the same directory first, then swapped in atomically, so a crash or
    /// power loss mid-write can never leave a half-written/corrupt ini file. The temp file
    /// is cleaned up even if the swap itself fails.
    /// </summary>
    public static void WriteToFile(IniDocument document, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var tempPath = Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, WriteToString(document));

            if (File.Exists(filePath))
            {
                // File.Replace does the atomic swap; ignoreMetadataErrors so odd ACL/attribute
                // setups on the original file don't turn a successful write into an exception.
                File.Replace(tempPath, filePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
