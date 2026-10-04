using NaturalGasPipeline.Core.Thermodynamics;

namespace NaturalGasPipeline.Tests.Thermodynamics;

internal static class CompressibilityTestHelpers
{
    public static GasMixture MixtureOf(string fluid) => fluid switch
    {
        "Methane" => new GasMixture { Components = { StandardGasComponents.Methane } },
        "Nitrogen" => new GasMixture { Components = { StandardGasComponents.Nitrogen } },
        "TypicalNaturalGas" => NaturalGasMixtures.TypicalNaturalGas,
        _ => throw new ArgumentOutOfRangeException(nameof(fluid), fluid, null)
    };

    public static ICompressibilityModel ModelFor(string eos, GasMixture mixture) => eos switch
    {
        "PR" => new PengRobinsonModel(mixture),
        "SRK" => new SoaveRedlichKwongModel(mixture),
        _ => throw new ArgumentOutOfRangeException(nameof(eos), eos, null)
    };

    public static double RelativeError(double actual, double expected) =>
        (actual - expected) / expected;
}
