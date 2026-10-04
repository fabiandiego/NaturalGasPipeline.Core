namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// The end pressures of a segment do not drive flow from inlet to outlet:
/// P1² − e^s·P2² ≤ 0. In a network this means the flow goes the other way
/// (or is zero); the network solver uses this to detect reversal.
/// </summary>
public sealed class NoForwardFlowException : InvalidOperationException
{
    public NoForwardFlowException(string message)
        : base(message)
    {
    }
}
