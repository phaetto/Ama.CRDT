namespace Modern.CRDT.Models;

using System.Text.Json.Nodes;

public readonly record struct CrdtDocument<T>(T? Data, JsonNode? Metadata) where T : class;