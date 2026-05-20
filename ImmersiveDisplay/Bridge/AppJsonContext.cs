using System.Text.Json.Serialization;
using ImmersiveDisplay.Models;

namespace ImmersiveDisplay.Bridge;

[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(FrontendLogsDto))]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(object))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true
)]
internal partial class AppJsonContext : JsonSerializerContext
{
}
