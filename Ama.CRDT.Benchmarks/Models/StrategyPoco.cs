namespace Ama.CRDT.Benchmarks.Models;

using System.Collections.Generic;
using System.Linq;
using Ama.CRDT.Attributes;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;

public class StrategyPoco
{
    [CrdtLwwStrategy]
    public string LwwValue { get; set; } = "initial";
    
    [CrdtCounterStrategy]
    public int Counter { get; set; }

    [CrdtGCounterStrategy]
    public uint GCounter { get; set; }

    [CrdtBoundedCounterStrategy(0, 100)]
    public int BoundedCounter { get; set; }

    [CrdtMaxWinsStrategy]
    public int MaxWins { get; set; } = 50;
    
    [CrdtMinWinsStrategy]
    public int MinWins { get; set; } = 50;
    
    [CrdtAverageRegisterStrategy]
    public decimal Average { get; set; }

    [CrdtGSetStrategy]
    public List<string> GSet { get; set; } = new();

    [CrdtTwoPhaseSetStrategy]
    public List<string> TwoPhaseSet { get; set; } = new() { "A", "B" };

    [CrdtLwwSetStrategy]
    public List<string> LwwSet { get; set; } = new() { "A", "B" };

    [CrdtOrSetStrategy]
    public List<string> OrSet { get; set; } = new() { "A", "B" };

    [CrdtArrayLcsStrategy]
    public List<string> LcsList { get; set; } = new() { "A", "B", "C" };

    [CrdtFixedSizeArrayStrategy(5)]
    public string?[] FixedArray { get; set; } = new string?[5] { "A", "B", "C", "D", "E" };

    [CrdtLseqStrategy]
    public List<string> LseqList { get; set; } = new() { "A", "B", "C" };

    [CrdtVoteCounterStrategy]
    public Dictionary<string, List<string>> Votes { get; set; } = new()
    {
        { "OptionA", new List<string> { "Voter1" } },
        { "OptionB", new List<string> { "Voter2" } }
    };

    [CrdtStateMachineStrategy(typeof(MyStateMachine))]
    public string State { get; set; } = "Created";

    [CrdtPriorityQueueStrategy(nameof(PrioItem.Priority))]
    public List<PrioItem> PrioQueue { get; set; } = new()
    {
        new() { Id = 1, Priority = 10, Value = "A" },
        new() { Id = 2, Priority = 20, Value = "B" }
    };
    
    [CrdtSortedSetStrategy]
    public List<PrioItem> SortedSet { get; set; } = new()
    {
        new() { Id = 1, Priority = 10, Value = "A" },
        new() { Id = 2, Priority = 20, Value = "B" }
    };

    [CrdtRgaStrategy]
    public List<string> RgaList { get; set; } = new() { "A", "B", "C" };

    [CrdtCounterMapStrategy]
    public Dictionary<string, int> CounterMap { get; set; } = new() { { "A", 1 }, { "B", 2 } };

    [CrdtLwwMapStrategy]
    public Dictionary<string, string> LwwMap { get; set; } = new() { { "A", "val1" }, { "B", "val2" } };

    [CrdtMaxWinsMapStrategy]
    public Dictionary<string, int> MaxWinsMap { get; set; } = new() { { "A", 10 }, { "B", 20 } };

    [CrdtMinWinsMapStrategy]
    public Dictionary<string, int> MinWinsMap { get; set; } = new() { { "A", 10 }, { "B", 20 } };

    [CrdtOrMapStrategy]
    public Dictionary<string, string> OrMap { get; set; } = new() { { "A", "val1" }, { "B", "val2" } };

    [CrdtGraphStrategy]
    public CrdtGraph Graph { get; set; } = new();

    [CrdtTwoPhaseGraphStrategy]
    public CrdtGraph TwoPhaseGraph { get; set; } = new();

    [CrdtReplicatedTreeStrategy]
    public CrdtTree Tree { get; set; } = new();

    public StrategyPoco Clone()
    {
        var clone = (StrategyPoco)MemberwiseClone();
        clone.GSet = new List<string>(GSet);
        clone.TwoPhaseSet = new List<string>(TwoPhaseSet);
        clone.LwwSet = new List<string>(LwwSet);
        clone.OrSet = new List<string>(OrSet);
        clone.LcsList = new List<string>(LcsList);
        clone.FixedArray = (string?[])FixedArray.Clone();
        clone.LseqList = new List<string>(LseqList);
        clone.Votes = Votes.ToDictionary(kvp => kvp.Key, kvp => new List<string>(kvp.Value));
        clone.PrioQueue = PrioQueue.Select(p => p with { }).ToList();
        clone.SortedSet = SortedSet.Select(p => p with { }).ToList();
        clone.RgaList = new List<string>(RgaList);
        
        clone.CounterMap = CounterMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        clone.LwwMap = LwwMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        clone.MaxWinsMap = MaxWinsMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        clone.MinWinsMap = MinWinsMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        clone.OrMap = OrMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        clone.Graph = new CrdtGraph 
        { 
            Vertices = new HashSet<object>(Graph.Vertices),
            Edges = new HashSet<Edge>(Graph.Edges)
        };
        
        clone.TwoPhaseGraph = new CrdtGraph 
        { 
            Vertices = new HashSet<object>(TwoPhaseGraph.Vertices),
            Edges = new HashSet<Edge>(TwoPhaseGraph.Edges)
        };
        
        clone.Tree = new CrdtTree 
        { 
            Nodes = Tree.Nodes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        return clone;
    }
}

public record PrioItem
{
    public int Id { get; set; }
    public int Priority { get; set; }
    public string? Value { get; set; }
}

public class MyStateMachine : IStateMachine<string>
{
    public bool IsValidTransition(string from, string to)
    {
        return (from == "Created" && to == "InProgress") || 
               (from == "InProgress" && to == "Completed");
    }
}