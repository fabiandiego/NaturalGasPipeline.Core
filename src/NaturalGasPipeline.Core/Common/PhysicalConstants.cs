namespace NaturalGasPipeline.Core.Common;

/// <summary>
/// Physical constants shared by every module. One definition, one source.
/// Add a constant here (with its source) instead of declaring it locally.
/// </summary>
public static class PhysicalConstants
{
    /// <summary>
    /// Molar gas constant R, J/(mol·K). Exact since the 2019 SI redefinition
    /// (R = N_A · k_B; CODATA 2018).
    /// </summary>
    public const double MolarGasConstant = 8.31446261815324;

    /// <summary>
    /// Standard acceleration of gravity g_n, m/s². Exact by definition
    /// (3rd CGPM, 1901).
    /// </summary>
    public const double StandardGravity = 9.80665;
}
