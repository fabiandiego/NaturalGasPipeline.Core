using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// The three physically distinct representations of a gas flow rate.
/// The standard conditions are carried by <see cref="StandardVolumetricFlow"/>.
/// </summary>
public class FlowRates
{
    public MassFlow MassFlow { get; set; }

    public ActualVolumetricFlow ActualVolumetricFlow { get; set; }

    public StandardVolumetricFlow? StandardVolumetricFlow { get; set; }
}
