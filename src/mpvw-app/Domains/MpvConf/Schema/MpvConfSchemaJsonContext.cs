using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace mpv_winui.Domains.MpvConf.Schema;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(List<MpvConfSchemaItem>))]
[JsonSerializable(typeof(List<MpvConfSchemaEnumValue>))]
[JsonSerializable(typeof(MpvConfSchemaEnumValue))]
internal partial class MpvConfSchemaJsonContext : JsonSerializerContext
{
}
