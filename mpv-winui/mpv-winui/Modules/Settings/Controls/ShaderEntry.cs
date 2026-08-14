namespace mpv_winui.Modules.Settings.Controls;

/// <summary>One row of the shader list editor.</summary>
public sealed class ShaderEntry
{
    public ShaderEntry(string path, bool enabled)
    {
        Path = path;
        Enabled = enabled;
    }

    public string Path { get; set; }

    public bool Enabled { get; set; }
}
