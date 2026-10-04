using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturalGasPipeline.Thermodynamics;

public static class CompressibilityModelFactory
{
    public static ICompressibilityModel Create(
        CompressibilityModelType modelType,
        GasMixture mixture,
        BinaryInteractionDatabase? interactionDatabase = null)
    {
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

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(modelType),
                    modelType,
                    "Unsupported compressibility model.")
        };
    }
}