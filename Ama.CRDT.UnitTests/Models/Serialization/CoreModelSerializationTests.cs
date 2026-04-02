namespace Ama.CRDT.UnitTests.Models.Serialization;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Ama.CRDT.Models;
using Shouldly;
using Xunit;

internal sealed class CoreSerializationTestModel : IEquatable<CoreSerializationTestModel>
{
    public string Data { get; set; } = string.Empty;

    public bool Equals(CoreSerializationTestModel? other)
    {
        if (other is null) return false;
        return Data == other.Data;
    }

    public override bool Equals(object? obj) => Equals(obj as CoreSerializationTestModel);
    public override int GetHashCode() => Data.GetHashCode();
}

public sealed class CoreModelSerializationTests
{
    [Fact]
    public void ApplyPatchResult_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Upsert, "value", new EpochTimestamp(123), 1, 2);
        var unapplied = new UnappliedOperation(op, CrdtOperationStatus.Obsolete);
        var document = new CrdtDocument<CoreSerializationTestModel>(new CoreSerializationTestModel { Data = "Test" }, new CrdtMetadata());
        var result = new ApplyPatchResult<CoreSerializationTestModel>(document, new[] { unapplied });

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(result, options);
        var deserialized = JsonSerializer.Deserialize<ApplyPatchResult<CoreSerializationTestModel>>(json, options);

        deserialized.Document.ShouldBe(result.Document);
        deserialized.UnappliedOperations.ShouldHaveSingleItem();
        deserialized.UnappliedOperations[0].ShouldBe(unapplied);
    }

    [Fact]
    public void JournaledOperation_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Remove, null, new EpochTimestamp(123), 1, 2);
        var journaled = new JournaledOperation("doc-1", op);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(journaled, options);
        var deserialized = JsonSerializer.Deserialize<JournaledOperation>(json, options);

        deserialized.ShouldBe(journaled);
    }

    [Fact]
    public void UnappliedOperation_ShouldSerializeAndDeserialize()
    {
        var op = new CrdtOperation(Guid.NewGuid(), "R1", "$.prop", OperationType.Increment, 5, new EpochTimestamp(123), 1, 2);
        var unapplied = new UnappliedOperation(op, CrdtOperationStatus.PathResolutionFailed);

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(unapplied, options);
        var deserialized = JsonSerializer.Deserialize<UnappliedOperation>(json, options);

        deserialized.ShouldBe(unapplied);
    }

    [Fact]
    public void DottedVersionVector_ShouldSerializeAndDeserialize()
    {
        var dvv = new DottedVersionVector(
            new Dictionary<string, long> { { "R1", 5 }, { "R2", 10 } },
            new Dictionary<string, ISet<long>> { { "R1", new HashSet<long> { 7, 9 } } }
        );

        var options = TestOptionsHelper.GetDefaultOptions();
        var json = JsonSerializer.Serialize(dvv, options);
        var deserialized = JsonSerializer.Deserialize<DottedVersionVector>(json, options);

        deserialized.ShouldNotBeNull();
        deserialized.Equals(dvv).ShouldBeTrue();
    }
}