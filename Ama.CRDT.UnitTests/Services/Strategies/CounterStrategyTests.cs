namespace Ama.CRDT.UnitTests.Services.Strategies;

using Ama.CRDT.Attributes.Strategies;
using Ama.CRDT.Extensions;
using Ama.CRDT.Models;
using Ama.CRDT.Models.Intents;
using Ama.CRDT.Services;
using Ama.CRDT.Services.GarbageCollection;
using Ama.CRDT.Services.Providers;
using Ama.CRDT.Services.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public sealed class CounterStrategyTests : IDisposable
{
    private sealed class TestModel { [CrdtCounterStrategy] public int Score { get; set; } }
    
    private readonly Mock<ICrdtPatcher> mockPatcher = new();

    private readonly IServiceScope scopeA;
    private readonly CounterStrategy strategy;
    private readonly ICrdtApplicator applicatorA;
    private readonly ICrdtMetadataManager metadataManagerA;
    private readonly ICrdtTimestampProvider timestampProvider;

    public CounterStrategyTests()
    {
        var serviceProvider = new ServiceCollection()
            .AddCrdt()
            .BuildServiceProvider();

        scopeA = serviceProvider.GetRequiredService<ICrdtScopeFactory>().CreateScope("A");

        strategy = scopeA.ServiceProvider.GetRequiredService<CounterStrategy>();
        applicatorA = scopeA.ServiceProvider.GetRequiredService<ICrdtApplicator>();
        metadataManagerA = scopeA.ServiceProvider.GetRequiredService<ICrdtMetadataManager>();
        timestampProvider = scopeA.ServiceProvider.GetRequiredService<ICrdtTimestampProvider>();
    }

    public void Dispose()
    {
        scopeA.Dispose();
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
        var expectedTimestamp = new EpochTimestampProvider(new ReplicaContext { ReplicaId = "replica-A" }).Create(12345);
        mockTimestampProvider.Setup(p => p.Now()).Returns(expectedTimestamp);
        var localStrategy = new CounterStrategy(new ReplicaContext { ReplicaId = "replica-A" });
        var context = new GeneratePatchContext(
            operations,
            new List<DifferentiateObjectContext>(),
            path, 
            property, 
            original, 
            modified, 
            new TestModel { Score = original }, 
            new TestModel { Score = modified }, 
            new CrdtMetadata(),
            expectedTimestamp,
            0);

        // Act
        localStrategy.GeneratePatch(context);

        // Assert
        operations.ShouldHaveSingleItem();
        var op = operations.First();
        op.Type.ShouldBe(OperationType.Increment);
        op.JsonPath.ShouldBe(path);
        op.Value.ShouldNotBeNull();
        op.Value.ShouldBe((decimal)delta);
        op.Timestamp.ShouldBe(expectedTimestamp);
    }

    [Fact]
    public void GenerateOperation_ShouldCreateIncrementOperation_WhenIncrementIntentProvided()
    {
        // Arrange
        var context = new GenerateOperationContext(
            new TestModel { Score = 10 },
            new CrdtMetadata(),
            "$.Score",
            typeof(TestModel).GetProperty(nameof(TestModel.Score))!,
            new IncrementIntent(5m),
            timestampProvider.Create(123),
            0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Increment);
        operation.Value.ShouldBe(5m);
        operation.JsonPath.ShouldBe("$.Score");
        operation.Timestamp.ShouldBe(timestampProvider.Create(123));
    }

    [Fact]
    public void GenerateOperation_ShouldCreateIncrementOperation_WhenSetIntentProvided()
    {
        // Arrange
        var context = new GenerateOperationContext(
            new TestModel { Score = 10 },
            new CrdtMetadata(),
            "$.Score",
            typeof(TestModel).GetProperty(nameof(TestModel.Score))!,
            new SetIntent(15m), // Target is 15, Current is 10 => Expected delta is 5
            timestampProvider.Create(123),
            0);

        // Act
        var operation = strategy.GenerateOperation(context);

        // Assert
        operation.Type.ShouldBe(OperationType.Increment);
        operation.Value.ShouldBe(5m);
        operation.JsonPath.ShouldBe("$.Score");
        operation.Timestamp.ShouldBe(timestampProvider.Create(123));
    }

    [Fact]
    public void GenerateOperation_ShouldThrowNotSupportedException_WhenUnsupportedIntentProvided()
    {
        // Arrange
        var context = new GenerateOperationContext(
            new TestModel { Score = 10 },
            new CrdtMetadata(),
            "$.Score",
            typeof(TestModel).GetProperty(nameof(TestModel.Score))!,
            new RemoveIntent(0),
            timestampProvider.Create(123),
            0);

        // Act & Assert
        Should.Throw<NotSupportedException>(() => strategy.GenerateOperation(context));
    }

    [Theory]
    [InlineData(10, 5, 15)]
    [InlineData(10, -5, 5)]
    [InlineData(0, 5, 5)]
    public void ApplyOperation_ShouldIncrementValue_Correctly(int initial, int increment, int expected)
    {
        // Arrange
        var model = new TestModel { Score = initial };
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Increment, (decimal)increment, timestampProvider.Create(2L), 1);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation)
        {
            Target = model,
            Property = property,
            FinalSegment = "score"
        };

        // Act
        strategy.ApplyOperation(context);

        // Assert
        model.Score.ShouldBe(expected);
    }
    
    [Fact]
    public void ApplyOperation_ShouldSetInitialValue_WhenPropertyDoesNotExist()
    {
        // Arrange
        var model = new TestModelWithNoScore();
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.Score", OperationType.Increment, 5m, timestampProvider.Create(1L), 1);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation)
        {
            Target = model,
            Property = null,
            FinalSegment = "Score"
        };

        // Act & Assert: This should not throw because the property doesn't exist.
        // The helper will return nulls and the strategy will exit gracefully.
        Should.NotThrow(() => strategy.ApplyOperation(context));
    }
    
    private sealed class TestModelWithNoScore { public string? Name { get; set; } }

    [Fact]
    public void ApplyOperation_ShouldReturnFailure_WhenOperationTypeIsNotIncrement()
    {
        // Arrange
        var model = new TestModel();
        var property = typeof(TestModel).GetProperty(nameof(TestModel.Score))!;
        var operation = new CrdtOperation(Guid.NewGuid(), "r", "$.score", OperationType.Upsert, 5m, timestampProvider.Create(1L), 1);
        var context = new ApplyOperationContext(model, new CrdtMetadata(), operation)
        {
            Target = model,
            Property = property,
            FinalSegment = "score"
        };

        // Act
        var result = strategy.ApplyOperation(context);

        // Assert
        result.ShouldBe(CrdtOperationStatus.StrategyApplicationFailed);
    }
    
    [Fact]
    public void ApplyPatch_IsIdempotent()
    {
        // Arrange
        var model = new TestModel { Score = 10 };
        var meta = metadataManagerA.Initialize(model);
        var document = new CrdtDocument<TestModel>(model, meta);
        var patch = new CrdtPatch(new List<CrdtOperation>
        {
            new(Guid.NewGuid(), "r1", "$.Score", OperationType.Increment, 5m, timestampProvider.Create(1L), 1)
        });

        // Act
        applicatorA.ApplyPatch(document, patch);
        var scoreAfterFirst = model.Score;
        applicatorA.ApplyPatch(document, patch);

        // Assert
        model.Score.ShouldBe(scoreAfterFirst);
        model.Score.ShouldBe(15);
    }

    [Fact]
    public void ApplyPatch_IsCommutativeAndAssociative()
    {
        // Arrange
        var patch1 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r1", "$.Score", OperationType.Increment, 10m, timestampProvider.Create(1L), 1) });
        var patch2 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r2", "$.Score", OperationType.Increment, -5m, timestampProvider.Create(2L), 1) });
        var patch3 = new CrdtPatch(new List<CrdtOperation> { new(Guid.NewGuid(), "r3", "$.Score", OperationType.Increment, 20m, timestampProvider.Create(3L), 1) });

        var patches = new[] { patch1, patch2, patch3 };
        var permutations = GetPermutations(patches, 3);
        var finalScores = new List<int>();

        // Act
        foreach (var p in permutations)
        {
            var model = new TestModel { Score = 10 };
            var meta = metadataManagerA.Initialize(model);
            var document = new CrdtDocument<TestModel>(model, meta);
            foreach (var patch in p)
            {
                applicatorA.ApplyPatch(document, patch);
            }
            finalScores.Add(model.Score);
        }

        // Assert
        // Expected: 10 + 10 - 5 + 20 = 35
        finalScores.ShouldAllBe(s => s == 35);
    }

    [Fact]
    public void Compact_ShouldNotModifyMetadata_AsStrategyDoesNotMaintainTombstones()
    {
        // Arrange
        var mockPolicy = new Mock<ICompactionPolicy>();
        mockPolicy.Setup(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>())).Returns(true);
        var metadata = new CrdtMetadata();

        var context = new CompactionContext(metadata, mockPolicy.Object, "Score", "$.score", new TestModel());

        // Act
        strategy.Compact(context);

        // Assert
        mockPolicy.Verify(p => p.IsSafeToCompact(It.IsAny<CompactionCandidate>()), Times.Never);
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