using NaturalGasPipeline.Core.Flow;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// A steady-state, isothermal pipeline flow equation written as a relation
/// between the standard flow Q and the pressure term
///
///   N = P1² − e^s · P2²   (Pa²)
///
/// with the elevation parameter s and the equivalent length
/// Le = L·(e^s − 1)/s already folded into <see cref="FlowEquationTerms"/>.
/// Both directions are required so that P1, P2, Q and D can all be solved
/// for (<see cref="GasFlowEquationSolver"/>).
///
/// To add an equation: implement this interface, state the source, the
/// field units of its constants and its range of application, convert to
/// those units only inside the implementation, and add tests against a
/// published worked example.
/// </summary>
public interface IGasFlowEquation
{
    /// <summary>Short, human-readable name for reports.</summary>
    string Name { get; }

    /// <summary>Q at base conditions for a given N &gt; 0 (Pa²).</summary>
    FlowEquationEvaluation CalculateFlow(double pressureTermPascalsSquared, FlowEquationTerms terms);

    /// <summary>N (Pa²) needed to carry the standard flow Q (&gt; 0).</summary>
    FlowEquationEvaluation CalculatePressureTerm(VolumeFlow standardFlow, FlowEquationTerms terms);

    /// <summary>Applicability warnings for this state (empty when none).</summary>
    IEnumerable<string> CheckApplicability(FlowEquationTerms terms, FlowEquationEvaluation evaluation);
}

/// <summary>
/// Everything an equation needs besides Q or N, in SI. Evaluated by the
/// solver at the average pressure P_avg = 2/3·(P1 + P2 − P1·P2/(P1 + P2)).
/// </summary>
public sealed record FlowEquationTerms
{
    public required Length InternalDiameter { get; init; }

    public required Length AbsoluteRoughness { get; init; }

    /// <summary>Le = L·(e^s − 1)/s (= L for a horizontal segment).</summary>
    public required Length EquivalentLength { get; init; }

    public required Temperature FlowingTemperature { get; init; }

    public required StandardFlowConditions BaseConditions { get; init; }

    public required double SpecificGravity { get; init; }

    public required MolarMass MolarMass { get; init; }

    /// <summary>Z at (P_avg, T).</summary>
    public required double CompressibilityFactor { get; init; }

    public required double ElevationParameter { get; init; }

    /// <summary>μ at (P_avg, T); null when the gas has no viscosity model.</summary>
    public required DynamicViscosity? Viscosity { get; init; }
}

/// <summary>Result of one evaluation of an equation, in both directions.</summary>
public readonly record struct FlowEquationEvaluation(
    double PressureTermPascalsSquared,
    VolumeFlow StandardFlow,
    double? TransmissionFactor,
    double? ReynoldsNumber);
