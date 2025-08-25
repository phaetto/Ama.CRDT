namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CounterStrategyTests
{
    private sealed class TestModel { [CrdtCounterStrategy] public int Score { get; set; } }
    
    private readonly CounterStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public CounterStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });

        strategy = new CounterStrategy(timestampProvider, optionsA);
        
        var lwwStrategy = new LwwStrategy(optionsA);
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, optionsA);
        var strategies = new ICrdtStrategy[] { lwwStrategy, strategy, arrayLcsStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        
        applicator = new CrdtApplicator(strategyManager);
        metadataManager = new CrdtMetadataManager(strategyManager, timestampProvider, comparerProvider);
    }

    [Theory]
    [InlineData(10, 15, 5)]
    [InlineData(10, 5, -5)]
    [InlineData(-5, 5, 10)]
    public void GeneratePatch_ShouldCreateIncrementOperation_WhenValueChanges(int original, int modified, int delta)
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var path = "$.score";
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;
        
        var mockTimestampProvider = new Mock<ICrdtTimestampProvider>();
        var expectedTimestamp = new EpochTimestamp(12345);
        mockTimestampProvider.Setup(p => p.Now()).Returns(expectedTimestamp);
        var localStrategy = new CounterStrategy(mockTimestampProvider.Object, Options.Create(new CrdtOptions { ReplicaId = "test" }));

        // Act
        localStrategy.GeneratePatch(mockPatcher.Object, operations, path, property, original, modified, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations.First();
        op.Type.ShouldBe(OperationType.Increment);
        op.JsonPath.ShouldBe(path);
        op.Value.ShouldNotBeNull();
        op.Value.ShouldBe((decimal)delta);
        op.Timestamp.ShouldBe(expectedTimestamp);
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var model = new TestModel { Score = initial };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Increment, (decimal)increment, new EpochTimestamp(2L));

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        model.Score.ShouldBe(expected);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var model = new TestModelWithNoScore();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, 5m, new EpochTimestamp(1L));

        // Act & Assert: This should not throw because the property doesn't exist.
        // The helper will return nulls and the strategy will exit gracefully.
        Should.NotThrow(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
    
    private sealed class TestModelWithNoScore { public string? Name { get; set; } }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var model = new TestModel();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Upsert, 5m, new EpochTimestamp(1L));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent_WithSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Score = 10 };
        var meta = metadataManager.Initialize(model);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Score", OperationType.Increment, 5m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(model, patch, meta);
        var scoreAfterFirst = model.Score;
        applicator.ApplyPatch(model, patch, meta);

        // Assert
        model.Score.ShouldBe(scoreAfterFirst);
        model.Score.ShouldBe(15);
    }

    [Fact]
    public void ApplyPatch_IsNotIdempotent_WithoutSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Score = 10 };
        var meta = metadataManager.Initialize(model);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Score", OperationType.Increment, 5m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(model, patch, meta);
        model.Score.ShouldBe(15);

        // Clear SeenExceptions to simulate re-application
        meta.SeenExceptions.Clear();
        applicator.ApplyPatch(model, patch, meta);

        // Assert
        // The increment is applied a second time, proving the strategy is not idempotent.
        model.Score.ShouldBe(20);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Score", OperationType.Increment, 10m, new EpochTimestamp(1L)) });
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Score", OperationType.Increment, -5m, new EpochTimestamp(2L)) });
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Score", OperationType.Increment, 20m, new EpochTimestamp(3L)) });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalScores = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Score = 10 };
            var meta = metadataManager.Initialize(model);
            foreach (var patch in p)
            {
                applicator.ApplyPatch(model, patch, meta);
            }
            finalScores.Add(model.Score);
        }

        // Assert
        // Expected: 10 + 10 - 5 + 20 = 35
        finalScores.ShouldAllBe(s => s == 35);
    }
    
    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });
        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
}