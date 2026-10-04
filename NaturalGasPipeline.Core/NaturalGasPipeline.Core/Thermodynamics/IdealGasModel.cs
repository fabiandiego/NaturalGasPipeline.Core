using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public class IdealGasModel : ICompressibilityModel
{
    public double Calculate(
        Pressure pressure,
        Temperature temperature)
    {
        return 1.0;
    }
}