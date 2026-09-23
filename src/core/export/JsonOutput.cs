using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace core.export;

/// <summary>
/// The one place that decides what spo's JSON looks like: camelCase, indented, absent values left
/// out rather than written as null, and non-ASCII escaped (æ) so the output survives any
/// console code page or shell redirection intact.
/// </summary>
public static class JsonOutput
{

    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc
    };

    public static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, Settings);
    }

}
