namespace Ama.CRDT.Attributes.Decorators;

using Ama.CRDT.Services.Strategies.Decorators;
using System;

/// <summary>
/// A decorator attribute that requires a specified number of approvals (quorum) 
/// from different replicas before applying the underlying CRDT operation.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CrdtApprovalQuorumAttribute : CrdtStrategyDecoratorAttribute
{
    /// <summary>
    /// Gets the number of approvals required to apply the operation.
    /// </summary>
    public int QuorumSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CrdtApprovalQuorumAttribute"/> class.
    /// </summary>
    /// <param name="quorumSize">The number of replica approvals required.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quorumSize"/> is less than 1.</exception>
    public CrdtApprovalQuorumAttribute(int quorumSize) : base(typeof(ApprovalQuorumStrategy))
    {
        if (quorumSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quorumSize), "Quorum size must be at least 1.");
        }
        
        QuorumSize = quorumSize;
    }
}