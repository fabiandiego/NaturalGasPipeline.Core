using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// An empirical Z model with a published range of validity.
/// Outside the range the model still returns a value; callers report a
/// warning (as <c>IsothermalGasFlowSolver</c> does) rather than fail.
/// </summary>
public interface ICompressibilityCorrelation : ICompressibilityModel
{
    /// <summary>Short, human-readable model name for reports.</summary>
    string Name { get; }

    /// <summary>True when (P, T) lies inside the range the correlation was fitted on.</summary>
    bool IsWithinValidatedRange(Pressure pressure, Temperature temperature);
}
