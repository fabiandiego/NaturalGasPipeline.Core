using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public interface ICompressibilityModel
{
    double Calculate(
        Pressure pressure,
        Temperature temperature);
}