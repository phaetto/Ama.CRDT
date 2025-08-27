namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes;
using Ama.CRDT.Models;
using Ama.CRDT.Services;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class GCounterStrategyTests
{
    private sealed class TestModel { [CrdtGCounterStrategy] public int Count { get; set; } }

    private readonly GCounterStrategy strategy;
    private readonly Mock<ICrdtPatcher> mockPatcher = new();
    private readonly List<CrdtOperation> operations = new();
    
    private readonly CrdtApplicator applicator;
    private readonly CrdtMetadataManager metadataManager;

    public GCounterStrategyTests()
    {
        var timestampProvider = new EpochTimestampProvider();
        var optionsA = Options.Create(new CrdtOptions { ReplicaId = "A" });
        
        strategy = new GCounterStrategy(timestampProvider, optionsA);

        var lwwStrategy = new LwwStrategy(optionsA);
        var comparerProvider = new ElementComparerProvider(Enumerable.Empty<IElementComparer>());
        var arrayLcsStrategy = new ArrayLcsStrategy(comparerProvider, timestampProvider, optionsA);
        var strategies = new ICrdtStrategy[] { lwwStrategy, strategy, arrayLcsStrategy };
        var strategyManager = new CrdtStrategyProvider(strategies);
        
        applicator = new CrdtApplicator(strategyManager);
        metadataManager = new CrdtMetadataManager(strategyManager, timestampProvider, comparerProvider);
    }

    [Fact]
    public void GeneratePatch_ShouldCreateIncrement_WhenValueIncreases()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Count))!;
        
        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.count", property, 10, 15, new TestModel { Count = 10 }, new TestModel { Count = 15 }, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        var op = operations.ShouldHaveSingleItem();
        op.Type.ShouldBe(OperationType.Increment);
        op.Value.ShouldBe(5m);
    }

    [Fact]
    public void GeneratePatch_ShouldDoNothing_WhenValueDecreases()
    {
        // Arrange
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Count))!;
        
        // Act
        strategy.GeneratePatch(mockPatcher.Object, operations, "$.count", property, 10, 5, new TestModel { Count = 10 }, new TestModel { Count = 5 }, new CrdtMetadata(), new CrdtMetadata());

        // Assert
        operations.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyOperation_ShouldIncrementValue_WithPositiveDelta()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Increment, 5m, new EpochTimestamp(2L));

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        model.Count.ShouldBe(15);
    }
    
    [Fact]
    public void ApplyOperation_ShouldIgnore_NegativeDelta()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Increment, -5m, new EpochTimestamp(2L));

        // Act
        strategy.ApplyOperation(model, new CrdtMetadata(), operation);

        // Assert
        model.Count.ShouldBe(10);
    }
    
    [Fact]
    public void ApplyOperation_ShouldThrow_ForNonIncrementOperation()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.count", OperationType.Upsert, 15, new EpochTimestamp(2L));

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => strategy.ApplyOperation(model, new CrdtMetadata(), operation));
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent_WithSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var meta = metadataManager.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Count", OperationType.Increment, 5m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(document, patch);
        var countAfterFirst = model.Count;
        applicator.ApplyPatch(document, patch);

        // Assert
        model.Count.ShouldBe(countAfterFirst);
        model.Count.ShouldBe(15);
    }

    [Fact]
    public void ApplyPatch_IsNotIdempotent_WithoutSeenExceptionsCheck()
    {
        // Arrange
        var model = new TestModel { Count = 10 };
        var meta = metadataManager.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Count", OperationType.Increment, 5m, new EpochTimestamp(1L))
        });

        // Act
        applicator.ApplyPatch(document, patch);
        model.Count.ShouldBe(15);

        // Clear SeenExceptions to simulate re-application
        meta.SeenExceptions.Clear();
        applicator.ApplyPatch(document, patch);

        // Assert
        // The increment is applied a second time, proving the strategy is not idempotent.
        model.Count.ShouldBe(20);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Count", OperationType.Increment, 10m, new EpochTimestamp(1L)) });
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Count", OperationType.Increment, 5m, new EpochTimestamp(2L)) });
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Count", OperationType.Increment, 20m, new EpochTimestamp(3L)) });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalCounts = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Count = 10 };
            var meta = metadataManager.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in p)
            {
                applicator.ApplyPatch(document, patch);
            }
            finalCounts.Add(model.Count);
        }

        // Assert
        // Expected: 10 + 10 + 5 + 20 = 45
        finalCounts.ShouldAllBe(s => s == 45);
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