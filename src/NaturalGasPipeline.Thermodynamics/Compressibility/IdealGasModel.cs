using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

public class IdealGasModel : ICompressibilityModel
{
    public double Calculate(
        Pressure pressure,
        Temperature temperature)
    {
        return 1.0;
    }
}