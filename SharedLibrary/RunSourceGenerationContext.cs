using System.Text.Json.Serialization;

namespace SharedLibrary;

[JsonSerializable(typeof(RunConfig))]
public partial class RunSourceGenerationContext : JsonSerializerContext;