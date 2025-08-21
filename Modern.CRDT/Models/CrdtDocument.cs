namespace Modern.CRDT.Models;

using System.Text.Json.Nodes;

public readonly record struct CrdtDocument(JsonNode? Data, JsonNode? Metadata);