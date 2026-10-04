using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

public interface ICompressibilityModel
{
    double Calculate(
        Pressure pressure,
        Temperature temperature);
}