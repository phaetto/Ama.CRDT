using System.Text.Json.Nodes;

namespace Modern.CRDT.Models;

public readonly record struct CrdtOperation(string JsonPath, OperationType Type, JsonNode? Value, long Timestamp);