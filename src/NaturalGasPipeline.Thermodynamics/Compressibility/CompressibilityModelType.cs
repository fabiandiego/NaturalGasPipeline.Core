namespace NaturalGasPipeline.Thermodynamics;

/// <summary>Z models that can be built from a gas mixture of known composition.</summary>
public enum CompressibilityModelType
{
    IdealGas,
    PengRobinson,
    SoaveRedlichKwong,

    /// <summary>DAK with Kay pseudo-criticals of the mixture.</summary>
    DranchukAbouKassem,

    /// <summary>Hall-Yarborough with Kay pseudo-criticals of the mixture.</summary>
    HallYarborough,

    /// <summary>Papay with Kay pseudo-criticals of the mixture.</summary>
    Papay
}

/// <summary>Correlations of the generalized Z chart, Z = Z(Tpr, Ppr).</summary>
public enum CorrespondingStatesModelType
{
    DranchukAbouKassem,
    HallYarborough,
    Papay
}
