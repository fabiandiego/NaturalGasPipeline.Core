using NaturalGasPipeline.Core.Common;
using NaturalGasPipeline.Hydraulics;
using UnitsNet;

namespace NaturalGasPipeline.Correlations;

/// <summary>
/// General Flow Equation in USCS field units (Menon, Gas Pipeline Hydraulics):
///
///   Q = 38.77 · F · (Tb/Pb) · [ (P1² − e^s·P2²) / (G · Tf · Le · Z) ]^0.5 · D^2.5
///
/// with the transmission factor F = 2/√f (Darcy f) from any
/// <see cref="IFrictionFactorModel"/>:
/// - Colebrook-White, Haaland, Swamee-Jain, Churchill → "General Flow";
/// - <see cref="AgaFullyTurbulentModel"/> or <see cref="AgaTransmissionFactorModel"/> → "AGA";
/// - <see cref="ConstantTransmissionFactorModel"/> / <see cref="ConstantDarcyFrictionModel"/> → user F or f.
///
/// F depends on Re = 4·ṁ/(π·D·μ), which depends on Q: given N, Q and F are
/// found by fixed-point iteration; given Q, F is direct. This is the same
/// physics as <see cref="IsothermalGasFlowSolver"/> (SI, exact constants);
/// the field form is kept to reproduce published examples. The constant
/// 38.77 is reproduced exactly by CED course P06-001, Example 12
/// (P1 = 1180.44 psig), see tests.
/// </summary>
public sealed class GeneralFlowEquation : IGasFlowEquation
{
    private const double Coefficient = 38.77;

    private readonly IFrictionFactorModel _friction;
    private readonly double _relativeTolerance;
    private readonly int _maxIterations;

    public GeneralFlowEquation(
        IFrictionFactorModel frictionModel,
        double relativeTolerance = 1e-12,
        int maxIterations = 100)
    {
        _friction = frictionModel ?? throw new ArgumentNullException(nameof(frictionModel));

        if (!double.IsFinite(relativeTolerance) || relativeTolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance), relativeTolerance, "Tolerance must be positive.");
        }

        if (maxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "At least one iteration is required.");
        }

        _relativeTolerance = relativeTolerance;
        _maxIterations = maxIterations;
    }

    public string Name => $"General Flow ({_friction.Name})";

    public IFrictionFactorModel FrictionModel => _friction;

    public FlowEquationEvaluation CalculateFlow(double pressureTermPascalsSquared, FlowEquationTerms terms)
    {
        Guard.NotNull(terms);

        if (!double.IsFinite(pressureTermPascalsSquared) || pressureTermPascalsSquared <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pressureTermPascalsSquared), pressureTermPascalsSquared, "Pressure term must be finite and greater than zero.");
        }

        DynamicViscosity mu = RequireViscosity(terms);
        double flowPerTransmissionFactor = FlowPerUnitTransmissionFactor(pressureTermPascalsSquared, terms);

        // Start from the fully rough value when the pipe is rough, else 20.
        double relativeRoughness = RelativeRoughness(terms);
        double f = relativeRoughness > 0.0
            ? AgaFullyTurbulentModel.TransmissionFactor(relativeRoughness)
            : 20.0;

        for (int i = 0; i < _maxIterations; i++)
        {
            VolumeFlow q = FieldUnits.FromCubicFeetPerDay(f * flowPerTransmissionFactor);
            double re = FieldUnits.Reynolds(q, terms, mu);
            double updated = TransmissionFactor(re, relativeRoughness);

            if (System.Math.Abs(updated - f) <= _relativeTolerance * f)
            {
                VolumeFlow converged = FieldUnits.FromCubicFeetPerDay(updated * flowPerTransmissionFactor);
                return new FlowEquationEvaluation(
                    pressureTermPascalsSquared, converged, updated, FieldUnits.Reynolds(converged, terms, mu));
            }

            f = updated;
        }

        throw new InvalidOperationException(
            $"{Name}: transmission factor did not converge in {_maxIterations} iterations.");
    }

    public FlowEquationEvaluation CalculatePressureTerm(VolumeFlow standardFlow, FlowEquationTerms terms)
    {
        Guard.NotNull(terms);

        double q = FieldUnits.CubicFeetPerDay(standardFlow);

        if (!double.IsFinite(q) || q <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardFlow), q, "Standard flow must be finite and greater than zero.");
        }

        double re = FieldUnits.Reynolds(standardFlow, terms, RequireViscosity(terms));
        double transmission = TransmissionFactor(re, RelativeRoughness(terms));

        // N = [Q / (38.77·F·(Tb/Pb)·D^2.5)]² · G·Tf·Le·Z
        double ratio = q / (Coefficient * transmission * FieldUnits.BaseRatio(terms.BaseConditions)
                            * System.Math.Pow(terms.InternalDiameter.Inches, 2.5));

        double n = ratio * ratio * Denominator(terms);

        return new FlowEquationEvaluation(FieldUnits.PascalsSquared(n), standardFlow, transmission, re);
    }

    public IEnumerable<string> CheckApplicability(FlowEquationTerms terms, FlowEquationEvaluation evaluation)
    {
        Guard.NotNull(terms);

        if (evaluation.ReynoldsNumber is double re
            && !_friction.IsWithinValidatedRange(re, RelativeRoughness(terms)))
        {
            yield return $"{Name}: friction model used outside its validated range (Re = {re:E3}).";
        }
    }

    // Q / F = 38.77·(Tb/Pb)·[N/(G·Tf·Le·Z)]^0.5·D^2.5, in ft³/day
    private static double FlowPerUnitTransmissionFactor(double pressureTermPascalsSquared, FlowEquationTerms terms) =>
        Coefficient
        * FieldUnits.BaseRatio(terms.BaseConditions)
        * System.Math.Sqrt(FieldUnits.PsiSquared(pressureTermPascalsSquared) / Denominator(terms))
        * System.Math.Pow(terms.InternalDiameter.Inches, 2.5);

    // G · Tf · Le · Z
    private static double Denominator(FlowEquationTerms terms) =>
        terms.SpecificGravity
        * terms.FlowingTemperature.DegreesRankine
        * terms.EquivalentLength.Miles
        * terms.CompressibilityFactor;

    private double TransmissionFactor(double reynoldsNumber, double relativeRoughness) =>
        FrictionFactorModelExtensions.TransmissionFactorFromDarcy(
            _friction.CalculateDarcyFrictionFactor(reynoldsNumber, relativeRoughness));

    private static double RelativeRoughness(FlowEquationTerms terms) =>
        FrictionFactorModelExtensions.RelativeRoughness(terms.InternalDiameter, terms.AbsoluteRoughness);

    private DynamicViscosity RequireViscosity(FlowEquationTerms terms) =>
        terms.Viscosity ?? throw new InvalidOperationException(
            $"{Name} needs a viscosity model (FlowEquationGas.Viscosity) to compute the Reynolds number.");
}
