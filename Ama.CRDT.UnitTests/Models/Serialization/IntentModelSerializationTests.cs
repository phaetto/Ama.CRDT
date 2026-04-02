namespace Ama.CRDT.UnitTests.Models.Serialization;

using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Models.Intents.Decorators;
using Shouldly;
using Xunit;

public sealed class IntentModelSerializationTests
{
    private T SerializeAndDeserialize<T>(T intent)
    {
        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(intent, options);
        return JsonSerializer.Deserialize<T>(json, options)!;
    }

    [Fact]
    public void AddEdgeIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new AddEdgeIntent(new Edge("A", "B", "data"))).ShouldBe(new AddEdgeIntent(new Edge("A", "B", "data")));
        
    [Fact] 
    public void AddIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new AddIntent("value")).ShouldBe(new AddIntent("value"));
        
    [Fact] 
    public void AddNodeIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new AddNodeIntent(new TreeNode { Id = "N1", Value = "V", ParentId = "P" })).Node.Id.ShouldBe("N1");
        
    [Fact] 
    public void AddVertexIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new AddVertexIntent("V1")).ShouldBe(new AddVertexIntent("V1"));
        
    [Fact] 
    public void ClearIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new ClearIntent()).ShouldBe(new ClearIntent());
        
    [Fact] 
    public void IncrementIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new IncrementIntent(5)).ShouldBe(new IncrementIntent(5));
        
    [Fact] 
    public void InsertIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new InsertIntent(2, "val")).ShouldBe(new InsertIntent(2, "val"));
        
    [Fact] 
    public void MapIncrementIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new MapIncrementIntent("key", 10)).ShouldBe(new MapIncrementIntent("key", 10));
        
    [Fact] 
    public void MapRemoveIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new MapRemoveIntent("key")).ShouldBe(new MapRemoveIntent("key"));
        
    [Fact] 
    public void MapSetIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new MapSetIntent("key", "val")).ShouldBe(new MapSetIntent("key", "val"));
        
    [Fact] 
    public void MoveNodeIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new MoveNodeIntent("N1", "P1")).ShouldBe(new MoveNodeIntent("N1", "P1"));
        
    [Fact] 
    public void RemoveEdgeIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new RemoveEdgeIntent(new Edge("A", "B", "data"))).ShouldBe(new RemoveEdgeIntent(new Edge("A", "B", "data")));
        
    [Fact] 
    public void RemoveIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new RemoveIntent(3)).ShouldBe(new RemoveIntent(3));
        
    [Fact] 
    public void RemoveNodeIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new RemoveNodeIntent("N1")).ShouldBe(new RemoveNodeIntent("N1"));
        
    [Fact] 
    public void RemoveValueIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new RemoveValueIntent("val")).ShouldBe(new RemoveValueIntent("val"));
        
    [Fact] 
    public void RemoveVertexIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new RemoveVertexIntent("V1")).ShouldBe(new RemoveVertexIntent("V1"));
        
    [Fact] 
    public void SetIndexIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new SetIndexIntent(1, "val")).ShouldBe(new SetIndexIntent(1, "val"));
        
    [Fact] 
    public void SetIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new SetIntent("val")).ShouldBe(new SetIntent("val"));
        
    [Fact] 
    public void VoteIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new VoteIntent("user1", "optA")).ShouldBe(new VoteIntent("user1", "optA"));
        
    [Fact] 
    public void EpochClearIntent_ShouldSerializeAndDeserialize() => 
        SerializeAndDeserialize(new EpochClearIntent()).ShouldBe(new EpochClearIntent());
}