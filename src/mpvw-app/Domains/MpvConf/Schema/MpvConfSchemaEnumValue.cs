using System.Text.Json.Serialization;

namespace mpv_winui.Domains.MpvConf.Schema;

public sealed class MpvConfSchemaEnumValue
{
    [JsonPropertyName("value")]
    public string Value
    {
        get;
        set;
    } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Desc
    {
        get;
        set;
    }
}