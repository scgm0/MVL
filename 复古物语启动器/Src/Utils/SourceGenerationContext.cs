using System.Text.Json.Serialization;

namespace 复古物语启动器.Utils;

[JsonSerializable(typeof(SharedLibrary.RunConfig))]
public partial class SourceGenerationContext : JsonSerializerContext;