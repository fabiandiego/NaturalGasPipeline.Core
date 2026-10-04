using UnitsNet;

namespace NaturalGasPipeline.Core.Thermodynamics;

public class GasComponent
{
    public string Name { get; set; } = string.Empty;

    public double MoleFraction { get; set; }

    public MolarMass MolarMass { get; set; }

    public Temperature CriticalTemperature { get; set; }

    public Pressure CriticalPressure { get; set; }

    public double AcentricFactor { get; set; }
}
/*la densidad del gas .

Para un gas real:

$$ \rho = \frac{P\,M}{ZRT} $$

donde:

\(P\)= presión
\(M\)= masa molar
\(Z\)= factor de compresibilidad
\(R\)= constante universal de los gases
\(T\)= temperatura

*/

