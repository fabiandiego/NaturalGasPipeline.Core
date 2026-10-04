namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// The requested flow cannot pass: no physical outlet pressure exists
/// (P2² ≤ 0) for the given inlet pressure. Distinguished from other
/// <see cref="InvalidOperationException"/>s so that searches over the flow
/// (e.g. finding the flow for given end pressures) can treat it as
/// "too much flow" instead of as a failure.
/// </summary>
public sealed class FlowCapacityExceededException : InvalidOperationException
{
    public FlowCapacityExceededException(string message)
        : base(message)
    {
    }
}
