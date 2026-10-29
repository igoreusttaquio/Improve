using System.Text.Json;
using System.Text.Json.Serialization;
using Improve.Core.Models;

namespace Improve.Core.Storage;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ExtensionsDocument))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
