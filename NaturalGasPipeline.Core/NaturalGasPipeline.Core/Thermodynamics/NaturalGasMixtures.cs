using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturalGasPipeline.Core.Thermodynamics;

public static class NaturalGasMixtures
{
    public static GasMixture TypicalNaturalGas => new()
    {
        Components =
        {
            CreateComponent(
                StandardGasComponents.Methane,
                0.900),

            CreateComponent(
                StandardGasComponents.Ethane,
                0.060),

            CreateComponent(
                StandardGasComponents.Propane,
                0.020),

            CreateComponent(
                StandardGasComponents.Nitrogen,
                0.015),

            CreateComponent(
                StandardGasComponents.CarbonDioxide,
                0.005)
        }
    };

    private static GasComponent CreateComponent(
        GasComponent component,
        double moleFraction)
    {
        component.MoleFraction = moleFraction;

        return component;
    }
}