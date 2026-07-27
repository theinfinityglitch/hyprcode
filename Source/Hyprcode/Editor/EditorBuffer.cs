using System.Text;

namespace Hyprcode.Editor;

public sealed class EditorBuffer
{
    public required string Path { get; init; }
    public string Text { get; set; } = "";
    public bool IsDirty { get; set; }
    public string? LoadError { get; set; }

    public static EditorBuffer Load(string path)
    {
        var buffer = new EditorBuffer { Path = path };
        try
        {
            buffer.Text = File.ReadAllText(path);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or DecoderFallbackException
                        or ArgumentException
            )
        {
            buffer.LoadError = $"Couldn't open this file as text: {ex.Message}";
        }
        return buffer;
    }

    public void Save()
    {
        File.WriteAllText(Path, Text);
        IsDirty = false;
    }
}
