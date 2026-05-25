using System.Text.Json.Serialization;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Bridge;

[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(FrontendLogsDto))]
[JsonSerializable(typeof(InitialState))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(IconImportResult))]
[JsonSerializable(typeof(List<UrlEntryDto>))]
[JsonSerializable(typeof(UrlEntryDto))]
[JsonSerializable(typeof(UrlRequest))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true
)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
