using NaturalGasPipeline.Core.Common;
namespace NaturalGasPipeline.Thermodynamics;

public static class CompressibilityModelFactory
{
    /// <summary>
    /// Z model for a mixture of known composition. The corresponding-states
    /// correlations use Kay pseudo-criticals with no sour-gas correction;
    /// for CO₂/H₂S build the model from
    /// <see cref="WichertAzizCorrection"/> with <see cref="Create(CorrespondingStatesModelType, PseudoCriticalProperties)"/>.
    /// </summary>
    public static ICompressibilityModel Create(
        CompressibilityModelType modelType,
        GasMixture mixture,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
        Guard.NotNull(mixture);

        return modelType switch
        {
            CompressibilityModelType.IdealGas =>
                new IdealGasModel(),

            CompressibilityModelType.PengRobinson =>
                new PengRobinsonModel(
                    mixture,
                    interactionDatabase),

            CompressibilityModelType.SoaveRedlichKwong =>
                new SoaveRedlichKwongModel(
                    mixture,
                    interactionDatabase),

            CompressibilityModelType.DranchukAbouKassem =>
                Create(CorrespondingStatesModelType.DranchukAbouKassem, PseudoCriticalProperties.FromKay(mixture)),

            CompressibilityModelType.HallYarborough =>
                Create(CorrespondingStatesModelType.HallYarborough, PseudoCriticalProperties.FromKay(mixture)),

            CompressibilityModelType.Papay =>
                Create(CorrespondingStatesModelType.Papay, PseudoCriticalProperties.FromKay(mixture)),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(modelType),
                    modelType,
                    "Unsupported compressibility model.")
        };
    }

    /// <summary>
    /// Corresponding-states Z correlation for given pseudo-critical
    /// properties (from Kay, Sutton, Standing, with or without Wichert-Aziz).
    /// </summary>
    public static CorrespondingStatesCompressibilityModel Create(
        CorrespondingStatesModelType modelType,
        PseudoCriticalProperties pseudoCritical) => modelType switch
    {
        CorrespondingStatesModelType.DranchukAbouKassem => new DranchukAbouKassemModel(pseudoCritical),
        CorrespondingStatesModelType.HallYarborough => new HallYarboroughModel(pseudoCritical),
        CorrespondingStatesModelType.Papay => new PapayModel(pseudoCritical),
        _ => throw new ArgumentOutOfRangeException(
            nameof(modelType), modelType, "Unsupported corresponding-states model.")
    };

    /// <summary>
    /// Corresponding-states Z correlation for a gas known only by its
    /// specific gravity: γ → (Tpc, Ppc) by <paramref name="pseudoCriticalCorrelation"/>.
    /// </summary>
    public static CorrespondingStatesCompressibilityModel CreateFromSpecificGravity(
        CorrespondingStatesModelType modelType,
        double specificGravity,
        IPseudoCriticalCorrelation pseudoCriticalCorrelation)
    {
        Guard.NotNull(pseudoCriticalCorrelation);

        return Create(modelType, pseudoCriticalCorrelation.Calculate(specificGravity));
    }
}
