namespace Ama.CRDT.UnitTests.Models.Serialization;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Serialization;
using Shouldly;
using Xunit;

public sealed class CoreModelSerializationTests
{
    private sealed class TestModel : IEquatable<TestModel>
    {
        public string Data { get; set; } = string.Empty;

        public bool Equals(TestModel? other)
        {
            if (other is null) return false;
            return Data == other.Data;
        }

        public override bool Equals(object? obj) => Equals(obj as TestModel);
        public override int GetHashCode() => Data.GetHashCode();
    }

    [Fact]
    public void ApplyPatchResult_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Upsert, "value", new EpochTimestamp(123), 1, 2);
        var unapplied = new UnappliedOperation(op, CrdtOperationStatus.Obsolete);
        var document = new CrdtDocument<TestModel>(new TestModel { Data = "Test" }, new CrdtMetadata());
        var result = new ApplyPatchResult<TestModel>(document, new[] { unapplied });

        var json = JsonSerializer.Serialize(result, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<ApplyPatchResult<TestModel>>(json, CrdtJsonContext.DefaultOptions);

        deserialized.Document.ShouldBe(result.Document);
        deserialized.UnappliedOperations.ShouldHaveSingleItem();
        deserialized.UnappliedOperations[0].ShouldBe(unapplied);
    }

    [Fact]
    public void JournaledOperation_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Remove, null, new EpochTimestamp(123), 1, 2);
        var journaled = new JournaledOperation("doc-1", op);

        var json = JsonSerializer.Serialize(journaled, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<JournaledOperation>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldBe(journaled);
    }

    [Fact]
    public void UnappliedOperation_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Increment, 5, new EpochTimestamp(123), 1, 2);
        var unapplied = new UnappliedOperation(op, CrdtOperationStatus.PathResolutionFailed);

        var json = JsonSerializer.Serialize(unapplied, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<UnappliedOperation>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldBe(unapplied);
    }

    [Fact]
    public void DottedVersionVector_ShouldSerializeAndDeserialize()
    {
        var dvv = new DottedVersionVector(
            new Dictionary<string, long> { { "R1", 5 }, { "R2", 10 } },
            new Dictionary<string, ISet<long>> { { "R1", new HashSet<long> { 7, 9 } } }
        );

        var json = JsonSerializer.Serialize(dvv, CrdtJsonContext.DefaultOptions);
        var deserialized = JsonSerializer.Deserialize<DottedVersionVector>(json, CrdtJsonContext.DefaultOptions);

        deserialized.ShouldNotBeNull();
        deserialized.Equals(dvv).ShouldBeTrue();
    }
}