namespace Ama.CRDT.UnitTests.Services.Pooling;

using System;
using System.Text.Json;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Helpers;
using Ama.CRDT.Services.Pooling;
using Ama.CRDT.UnitTests.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public sealed class CrdtStringPoolTests
{
    [Fact]
    public void GetOrAdd_String_ShouldReturnSameReference()
    {
        // Arrange
        var pool = new CrdtStringPool();
        var s1 = new string("replica-1".ToCharArray());
        var s2 = new string("replica-1".ToCharArray());

        // Act
        var p1 = pool.GetOrAdd(s1);
        var p2 = pool.GetOrAdd(s2);

        // Assert
        ReferenceEquals(s1, s2).ShouldBeFalse();
        ReferenceEquals(p1, p2).ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_Span_ShouldReturnSameReferenceAsPooledString()
    {
        // Arrange
        var pool = new CrdtStringPool();
        var s1 = new string("$.user.name".ToCharArray());
        var pooled = pool.GetOrAdd(s1);

        ReadOnlySpan<char> span = "$.user.name".AsSpan();

        // Act
        var fromSpan = pool.GetOrAdd(span);

        // Assert
        ReferenceEquals(pooled, fromSpan).ShouldBeTrue();
    }

    [Fact]
    public void GetOrAdd_WhenExceedingMaxCapacity_ShouldNotPoolNewStrings()
    {
        // Arrange
        var pool = new CrdtStringPool(maxCapacity: 2);
        var first = pool.GetOrAdd(new string("first".ToCharArray()));
        var second = pool.GetOrAdd(new string("second".ToCharArray()));

        var thirdA = new string("third".ToCharArray());
        var thirdB = new string("third".ToCharArray());

        // Act
        var pThirdA = pool.GetOrAdd(thirdA);
        var pThirdB = pool.GetOrAdd(thirdB);

        // Assert
        ReferenceEquals(first, pool.GetOrAdd("first".AsSpan())).ShouldBeTrue();
        ReferenceEquals(second, pool.GetOrAdd("second".AsSpan())).ShouldBeTrue();
        ReferenceEquals(pThirdA, pThirdB).ShouldBeFalse();
    }

    [Fact]
    public void GetOrAdd_WithDynamicDictionaryKeyPath_ShouldSkipPooling()
    {
        // Arrange
        var pool = new CrdtStringPool();
        var dynamicPath1 = new string("$.users['guid-1234'].name".ToCharArray());
        var dynamicPath2 = new string("$.users['guid-1234'].name".ToCharArray());

        // Act
        var p1 = pool.GetOrAdd(dynamicPath1);
        var p2 = pool.GetOrAdd(dynamicPath2);

        // Assert
        ReferenceEquals(p1, p2).ShouldBeFalse();
    }

    [Fact]
    public void JsonDeserialization_ShouldPoolStructuralStringsInPatchAndMetadata()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt();
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredKeyedService<JsonSerializerOptions>("Ama.CRDT");
        var pool = sp.GetRequiredService<ICrdtStringPool>();

        var replicaId = pool.GetOrAdd(new string("replica-node-alpha".ToCharArray()));
        var shardId = pool.GetOrAdd(new string("shard-eu-west".ToCharArray()));
        var jsonPath = pool.GetOrAdd(new string("$.profile.displayName".ToCharArray()));

        var op = new CrdtOperation(Guid.NewGuid(), replicaId, jsonPath, OperationType.Upsert, "Alice", new CrdtTimestamp(100), 1, shardId, 1);
        var patch = new CrdtPatch([op]);

        var metadata = new CrdtMetadata();
        metadata.States[jsonPath] = new CausalTimestamp(new CrdtTimestamp(100), replicaId, 1);
        metadata.VersionVector[replicaId] = 1;

        var patchJson = JsonSerializer.Serialize(patch, options);
        var metadataJson = JsonSerializer.Serialize(metadata, options);

        // Act
        var deserializedPatch = JsonSerializer.Deserialize<CrdtPatch>(patchJson, options);
        var deserializedMetadata = JsonSerializer.Deserialize<CrdtMetadata>(metadataJson, options)!;

        // Assert
        var deserializedOp = deserializedPatch.Operations[0];
        ReferenceEquals(deserializedOp.ReplicaId, replicaId).ShouldBeTrue();
        ReferenceEquals(deserializedOp.ShardId, shardId).ShouldBeTrue();
        ReferenceEquals(deserializedOp.JsonPath, jsonPath).ShouldBeTrue();

        foreach (var key in deserializedMetadata.States.Keys)
        {
            ReferenceEquals(key, jsonPath).ShouldBeTrue();
            var state = (CausalTimestamp)deserializedMetadata.States[key];
            ReferenceEquals(state.ReplicaId, replicaId).ShouldBeTrue();
        }

        foreach (var key in deserializedMetadata.VersionVector.Keys)
        {
            ReferenceEquals(key, replicaId).ShouldBeTrue();
        }
    }

    [Fact]
    public void CrdtScopeFactory_And_RootJsonPath_ShouldUsePooledAndInternedInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddCrdt();
        services.AddCrdtAotContext<ServicesTestCrdtAotContext>();
        var sp = services.BuildServiceProvider();

        var pool = sp.GetRequiredService<ICrdtStringPool>();
        var scopeFactory = sp.GetRequiredService<ICrdtScopeFactory>();

        var dynamicReplicaId = new string("scoped-replica-1".ToCharArray());
        var expectedPooled = pool.GetOrAdd("scoped-replica-1");

        // Act
        using var scope = scopeFactory.CreateScope(dynamicReplicaId);
        var replicaContext = scope.ServiceProvider.GetRequiredService<ReplicaContext>();

        var context = new ServicesTestCrdtAotContext();
        var typeInfo = context.GetTypeInfo(typeof(CrdtPatcherTests.TestModel))!;
        var nameProp = typeInfo.Properties[nameof(CrdtPatcherTests.TestModel.Name)];

        var decoratorPath1 = MetadataPathHelper.GetDecoratorPath("$.name", "Epoch", pool);
        var decoratorPath2 = MetadataPathHelper.GetDecoratorPath("$.name", "Epoch", pool);

        // Assert
        ReferenceEquals(replicaContext.ReplicaId, expectedPooled).ShouldBeTrue();
        string.IsInterned(nameProp.RootJsonPath).ShouldNotBeNull();
        nameProp.RootJsonPath.ShouldBe("$.name");
        ReferenceEquals(decoratorPath1, decoratorPath2).ShouldBeTrue();
    }
}