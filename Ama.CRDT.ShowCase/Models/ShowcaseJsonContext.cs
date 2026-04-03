namespace Ama.CRDT.ShowCase.Models;

using System.Text.Json.Serialization;

/// <summary>
/// AOT JSON serialization context for the showcase models.
/// </summary>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(UserStats))]
[JsonSerializable(typeof(User))]
public partial class ShowcaseJsonContext : JsonSerializerContext
{
}