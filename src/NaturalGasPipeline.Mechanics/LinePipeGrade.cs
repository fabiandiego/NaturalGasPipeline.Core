using UnitsNet;

namespace NaturalGasPipeline.Mechanics;

/// <summary>
/// Line-pipe steel grade and its specified minimum yield strength (SMYS).
/// </summary>
public sealed class LinePipeGrade
{
    public LinePipeGrade(string name, Pressure smys)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Grade name is required.", nameof(name));
        }

        if (!double.IsFinite(smys.Pascals) || smys.Pascals <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(smys), "SMYS must be finite and greater than zero.");
        }

        Name = name;
        Smys = smys;
    }

    /// <summary>Grade designation, e.g. "L360 / X52".</summary>
    public string Name { get; }

    /// <summary>Specified minimum yield strength S.</summary>
    public Pressure Smys { get; }

    public override string ToString() => $"{Name} (SMYS {Smys.Megapascals:F0} MPa)";
}

/// <summary>
/// API 5L / ISO 3183 grades. The SI designation "L&lt;n&gt;" names the SMYS
/// in MPa, and that SI value is used here.
///
/// The older US-customary X-grade values (X52 = 52 000 psi = 358.5 MPa,
/// B = 35 000 psi = 241 MPa, X80 = 80 000 psi = 551.6 MPa) differ from the
/// L-designation by 0.2–1.7 %. Which one applies depends on the API 5L
/// edition the project adopts; review item M-3. A grade with any other SMYS
/// can be built with the <see cref="LinePipeGrade"/> constructor.
/// </summary>
[PendingReview("M-3", "SMYS taken from the L-designation (MPa); confirm against the adopted API 5L edition (PSL1/PSL2) and decide between SI and psi-based values.")]
public static class ApiLinePipeGrades
{
    public static readonly LinePipeGrade B = new("L245 / B", Pressure.FromMegapascals(245.0));
    public static readonly LinePipeGrade X42 = new("L290 / X42", Pressure.FromMegapascals(290.0));
    public static readonly LinePipeGrade X46 = new("L320 / X46", Pressure.FromMegapascals(320.0));
    public static readonly LinePipeGrade X52 = new("L360 / X52", Pressure.FromMegapascals(360.0));
    public static readonly LinePipeGrade X56 = new("L390 / X56", Pressure.FromMegapascals(390.0));
    public static readonly LinePipeGrade X60 = new("L415 / X60", Pressure.FromMegapascals(415.0));
    public static readonly LinePipeGrade X65 = new("L450 / X65", Pressure.FromMegapascals(450.0));
    public static readonly LinePipeGrade X70 = new("L485 / X70", Pressure.FromMegapascals(485.0));
    public static readonly LinePipeGrade X80 = new("L555 / X80", Pressure.FromMegapascals(555.0));

    /// <summary>All grades above, from lowest to highest SMYS (for UI lists).</summary>
    public static IReadOnlyList<LinePipeGrade> All { get; } = new[] { B, X42, X46, X52, X56, X60, X65, X70, X80 };
}
