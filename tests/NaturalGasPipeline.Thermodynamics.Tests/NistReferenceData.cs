namespace NaturalGasPipeline.Thermodynamics.Tests;

/// <summary>
/// Reference fluid properties from the NIST Chemistry WebBook,
/// "Thermophysical Properties of Fluid Systems"
/// (https://webbook.nist.gov/chemistry/fluid/), retrieved 2026-10-04.
///
/// Methane: Setzmann &amp; Wagner, J. Phys. Chem. Ref. Data 20(6), 1061 (1991).
/// Nitrogen: Span, Lemmon, Jacobsen, Wagner &amp; Yokozeki,
///           J. Phys. Chem. Ref. Data 29(6), 1361 (2000).
///
/// These reference equations of state reproduce measured densities to
/// roughly 0.03 % in this region, so they stand in for the real gas.
/// Values are densities as published; Z is derived in the tests with the
/// molar mass used by the same reference equation.
/// </summary>
public static class NistReferenceData
{
    /// <summary>Molar mass used by the Setzmann–Wagner equation, kg/mol.</summary>
    public const double MethaneMolarMass = 0.0160428;

    /// <summary>Molar mass used by the Span et al. equation, kg/mol.</summary>
    public const double NitrogenMolarMass = 0.02801348;

    /// <summary>
    /// Fluid, temperature (K), pressure (MPa), density (kg/m³).
    /// </summary>
    public static IEnumerable<object[]> PureFluidDensities =>
        new List<object[]>
        {
            new object[] { "Methane", 273.15, 1.0, 7.234333 },
            new object[] { "Methane", 273.15, 4.0, 31.17682 },
            new object[] { "Methane", 273.15, 7.0, 58.86624 },
            new object[] { "Methane", 273.15, 10.0, 90.04018 },

            new object[] { "Methane", 288.15, 1.0, 6.829216 },
            new object[] { "Methane", 288.15, 4.0, 29.00000 },
            new object[] { "Methane", 288.15, 7.0, 53.79515 },
            new object[] { "Methane", 288.15, 10.0, 80.85565 },

            new object[] { "Methane", 313.15, 1.0, 6.250718 },
            new object[] { "Methane", 313.15, 4.0, 26.07637 },
            new object[] { "Methane", 313.15, 7.0, 47.43201 },
            new object[] { "Methane", 313.15, 10.0, 69.97382 },

            new object[] { "Nitrogen", 288.15, 1.0, 11.72367 },
            new object[] { "Nitrogen", 288.15, 4.0, 47.12667 },
            new object[] { "Nitrogen", 288.15, 7.0, 82.47998 },
            new object[] { "Nitrogen", 288.15, 10.0, 117.2486 },
        };

    /// <summary>
    /// Fluid, temperature (K), pressure (MPa), density (kg/m³), viscosity (µPa·s).
    /// Same state points as <see cref="PureFluidDensities"/>, as reported by
    /// the NIST WebBook (transport-property correlations cited on its pages).
    /// </summary>
    public static IEnumerable<object[]> PureFluidViscosities =>
        new List<object[]>
        {
            new object[] { "Methane", 273.15, 1.0, 7.234333, 10.38720 },
            new object[] { "Methane", 273.15, 4.0, 31.17682, 11.03141 },
            new object[] { "Methane", 273.15, 7.0, 58.86624, 12.07535 },
            new object[] { "Methane", 273.15, 10.0, 90.04018, 13.62254 },

            new object[] { "Methane", 288.15, 1.0, 6.829216, 10.87902 },
            new object[] { "Methane", 288.15, 4.0, 29.00000, 11.47477 },
            new object[] { "Methane", 288.15, 7.0, 53.79515, 12.37903 },
            new object[] { "Methane", 288.15, 10.0, 80.85565, 13.64415 },

            new object[] { "Methane", 313.15, 1.0, 6.250718, 11.67829 },
            new object[] { "Methane", 313.15, 4.0, 26.07637, 12.21021 },
            new object[] { "Methane", 313.15, 7.0, 47.43201, 12.95603 },
            new object[] { "Methane", 313.15, 10.0, 69.97382, 13.93291 },

            new object[] { "Nitrogen", 288.15, 1.0, 11.72367, 17.46956 },
            new object[] { "Nitrogen", 288.15, 4.0, 47.12667, 18.01180 },
            new object[] { "Nitrogen", 288.15, 7.0, 82.47998, 18.71439 },
            new object[] { "Nitrogen", 288.15, 10.0, 117.2486, 19.56019 },
        };

    public static double MolarMassOf(string fluid) => fluid switch
    {
        "Methane" => MethaneMolarMass,
        "Nitrogen" => NitrogenMolarMass,
        _ => throw new ArgumentOutOfRangeException(nameof(fluid), fluid, null)
    };
}
