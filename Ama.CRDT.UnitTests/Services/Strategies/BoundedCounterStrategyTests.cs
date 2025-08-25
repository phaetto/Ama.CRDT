namespace Ama.CRDT.UnitTests.Services.Strategies;

using System.Collections.Generic;
using System.Reflection;
using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Strategies;
using Moq;
using Shouldly;
using Xunit;
using System.Linq;
using Microsoft.Extensions.Options;

public sealed class BoundedCounterStrategyTests
{
    private sealed class TestModel
    {
        [CrdtBoundedCounterStrategy(0, 100)]
        public int Level { get; set; }
    }
    
    private readonly BoundedCounterStrategy strategy;
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public BoundedCounterStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });

        strategy = new BoundedCounterStrategy(timestampProvider, optionsA);
        
        var lwwStrategy = new LwwStrategy(optionsA);
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, optionsA);
        var strategies = new ICrdtStrategy[] { lwwStrategy, strategy, arrayLcsStrategy };
        var strategyManager = new CrdtStrategyManager(strategies);
        
        applicator = new CrdtApplicator(strategyManager);
        metadataManager = new CrdtMetadataManager(strategyManager, timestampProvider, comparerProvider);
    }
    
    [Fact]
    public void GeneratePatch_ShouldCreateIncrementOperation_WhenValueChanges()
    {
        // Arrange
        var operations = new List<CrdtOperation>();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Level))!;
        
        // Act
        strategy.GeneratePatch(new Mock<ICrdtPatcher>().Object, operations, "$.level", property, 50, 60, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Increment);
        op.Value.ShouldBe(10m);
    }
    
    [Theory]
    [InlineData(50, 20, 70)]    // Normal increment
    [InlineData(50, -20, 30)]   // Normal decrement
    [InlineData(90, 20, 100)]   // Clamp to max
    [InlineData(10, -20, 0)]    // Clamp to min
    [InlineData(110, -5, 100)]  // Start above max, clamp
    [InlineData(-10, 5, 0)]     // Start below min, clamp
    public void ApplyOperation_ShouldClampValue_WithinBounds(int initial, int delta, int expected)
    {
        // Arrange
        var model = new TestModel { Level = initial };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.level", OperationType.Increment, (decimal)delta, new EpochTimestamp(2L));
        
        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);
        
        // Assert
        model.Level.ShouldBe(expected);
    }

    [Fact]
    public void ApplyOperation_ShouldThrow_WhenAttributeIsMissing()
    {
        // Arrange
        var model = new NoAttributeModel { Value = 50 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.value", OperationType.Increment, 10m, new EpochTimestamp(2L));
        
        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent_WithSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Level = 50 };
        var meta = metadataManager.Initialize(model);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Level", OperationType.Increment, 10m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(model, patch, meta);
        var scoreAfterFirst = model.Level;
        applicator.ApplyPatch(model, patch, meta);

        // Assert
        model.Level.ShouldBe(scoreAfterFirst);
        model.Level.ShouldBe(60);
    }

    [Fact]
    public void ApplyPatch_IsNotIdempotent_WithoutSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Level = 50 };
        var meta = metadataManager.Initialize(model);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Level", OperationType.Increment, 10m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(model, patch, meta);
        model.Level.ShouldBe(60);

        // Clear SeenExceptions to simulate re-application
        meta.SeenExceptions.Clear();
        applicator.ApplyPatch(model, patch, meta);

        // Assert
        // The increment is applied a second time, proving the strategy is not idempotent.
        model.Level.ShouldBe(70);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Level", OperationType.Increment, 10m, new EpochTimestamp(1L)) });   // 50 -> 60
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Level", OperationType.Increment, -20m, new EpochTimestamp(2L)) }); // 60 -> 40
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Level", OperationType.Increment, 70m, new EpochTimestamp(3L)) });  // 40 -> 100 (clamped)

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalScores = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Level = 50 };
            var meta = metadataManager.Initialize(model);
            foreach (var patch in p)
            {
                applicator.ApplyPatch(model, patch, meta);
            }
            finalScores.Add(model.Level);
        }

        // Assert
        // Expected: 50 + 10 - 20 + 70 = 110, which clamps to 100.
        finalScores.ShouldAllBe(s => s == 100);
    }

    private IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });
        var enumerable = list as T[] ?? list.ToArray();
        return GetPermutations(enumerable, length - 1)
            .SelectMany(t => enumerable.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }
    
    private sealed class NoAttributeModel { public int Value { get; set; } }
}