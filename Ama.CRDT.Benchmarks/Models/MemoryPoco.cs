namespace Ama.CRDT.Benchmarks.Models;

using System;
using System.Collections.Generic;
using Ama.CRDT.Attributes.Strategies;

public sealed class WidePoco
{
    public Guid Id { get; set; }
    [CrdtLwwStrategy] public string Prop1 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop2 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop3 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop4 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop5 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop6 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop7 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop8 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop9 { get; set; } = string.Empty;
    [CrdtLwwStrategy] public string Prop10 { get; set; } = string.Empty;
    [CrdtCounterStrategy] public int Counter1 { get; set; }
    [CrdtCounterStrategy] public int Counter2 { get; set; }
    [CrdtCounterStrategy] public int Counter3 { get; set; }
    [CrdtCounterStrategy] public int Counter4 { get; set; }
    [CrdtCounterStrategy] public int Counter5 { get; set; }
    [CrdtMaxWinsStrategy] public long Max1 { get; set; }
    [CrdtMaxWinsStrategy] public long Max2 { get; set; }
    [CrdtMaxWinsStrategy] public long Max3 { get; set; }
    [CrdtMinWinsStrategy] public long Min1 { get; set; }
    [CrdtMinWinsStrategy] public long Min2 { get; set; }
}

public sealed class CollectionHeavyPoco
{
    public Guid Id { get; set; }

    [CrdtOrSetStrategy]
    public List<string> Tags { get; set; } = new();

    [CrdtLwwMapStrategy]
    public Dictionary<string, string> Attributes { get; set; } = new();

    [CrdtLseqStrategy]
    public List<string> Logs { get; set; } = new();
}