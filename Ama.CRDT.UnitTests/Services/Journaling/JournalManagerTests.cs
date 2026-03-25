namespace Ama.CRDT.UnitTests.Services.Journaling;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ama.CRDT.Models;
using Ama.CRDT.Services.Journaling;
using Moq;
using Shouldly;
using Xunit;

public sealed class JournalManagerTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenJournalIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new JournalManager(null!));
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldReturnEmpty_WhenRequirementIsNotBehind()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var requirement = new ReplicaSyncRequirement(); // IsBehind evaluates to false

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldReturnEmpty_WhenRequirementsByOriginIsNull()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = null!
        };

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldSkipOrigins_WhenHasMissingDataIsFalse()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "origin1", new OriginSyncRequirement { TargetContiguousVersion = 10, SourceContiguousVersion = 10 } }
            }
        };

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.ShouldBeEmpty();
        journalMock.Verify(j => j.GetOperationsByRangeAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        journalMock.Verify(j => j.GetOperationsByDotsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<long>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldYieldRangeOperations_WhenTargetKnownDotsIsNull()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "origin1", new OriginSyncRequirement 
                    { 
                        TargetContiguousVersion = 5,
                        SourceContiguousVersion = 10,
                        TargetKnownDots = null!
                    } 
                }
            }
        };

        var ops = new List<JournaledOperation>
        {
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 6 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 7 })
        };

        journalMock.Setup(j => j.GetOperationsByRangeAsync("origin1", 5L, 10L, It.IsAny<CancellationToken>()))
                   .Returns(AsAsyncEnumerable(ops));

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(o => o.Operation.GlobalClock == 6);
        result.ShouldContain(o => o.Operation.GlobalClock == 7);
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldYieldRangeOperations_ExcludingKnownDots()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "origin1", new OriginSyncRequirement 
                    { 
                        TargetContiguousVersion = 5,
                        SourceContiguousVersion = 10,
                        TargetKnownDots = new HashSet<long> { 7, 9 }
                    } 
                }
            }
        };

        var ops = new List<JournaledOperation>
        {
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 6 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 7 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 8 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 9 })
        };

        journalMock.Setup(j => j.GetOperationsByRangeAsync("origin1", 5L, 10L, It.IsAny<CancellationToken>()))
                   .Returns(AsAsyncEnumerable(ops));

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(o => o.Operation.GlobalClock == 6);
        result.ShouldContain(o => o.Operation.GlobalClock == 8);
        result.ShouldNotContain(o => o.Operation.GlobalClock == 7);
        result.ShouldNotContain(o => o.Operation.GlobalClock == 9);
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldYieldDotOperations_WhenSourceMissingDotsHasItems()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var missingDots = new HashSet<long> { 12, 15 };
        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "origin2", new OriginSyncRequirement 
                    { 
                        TargetContiguousVersion = 10,
                        SourceContiguousVersion = 10,
                        SourceMissingDots = missingDots
                    } 
                }
            }
        };

        var ops = new List<JournaledOperation>
        {
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 12 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 15 })
        };

        journalMock.Setup(j => j.GetOperationsByDotsAsync("origin2", missingDots, It.IsAny<CancellationToken>()))
                   .Returns(AsAsyncEnumerable(ops));

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(o => o.Operation.GlobalClock == 12);
        result.ShouldContain(o => o.Operation.GlobalClock == 15);
    }

    [Fact]
    public async Task GetMissingOperationsAsync_ShouldYieldBothRangeAndDotOperations()
    {
        // Arrange
        var journalMock = new Mock<ICrdtOperationJournal>();
        var sut = new JournalManager(journalMock.Object);

        var missingDots = new HashSet<long> { 11 };
        var requirement = new ReplicaSyncRequirement 
        { 
            RequirementsByOrigin = new Dictionary<string, OriginSyncRequirement>
            {
                { "origin3", new OriginSyncRequirement 
                    { 
                        TargetContiguousVersion = 5,
                        SourceContiguousVersion = 8,
                        TargetKnownDots = new HashSet<long> { 7 },
                        SourceMissingDots = missingDots
                    } 
                }
            }
        };

        var rangeOps = new List<JournaledOperation>
        {
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 6 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 7 }),
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 8 })
        };

        var dotOps = new List<JournaledOperation>
        {
            new JournaledOperation("doc1", new CrdtOperation { GlobalClock = 11 })
        };

        journalMock.Setup(j => j.GetOperationsByRangeAsync("origin3", 5L, 8L, It.IsAny<CancellationToken>()))
                   .Returns(AsAsyncEnumerable(rangeOps));

        journalMock.Setup(j => j.GetOperationsByDotsAsync("origin3", missingDots, It.IsAny<CancellationToken>()))
                   .Returns(AsAsyncEnumerable(dotOps));

        // Act
        var result = await ToListAsync(sut.GetMissingOperationsAsync(requirement));

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(o => o.Operation.GlobalClock == 6);
        result.ShouldContain(o => o.Operation.GlobalClock == 8);
        result.ShouldContain(o => o.Operation.GlobalClock == 11);
        result.ShouldNotContain(o => o.Operation.GlobalClock == 7);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> asyncEnumerable)
    {
        var list = new List<T>();
        await foreach (var item in asyncEnumerable.ConfigureAwait(false))
        {
            list.Add(item);
        }
        return list;
    }

    private static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}