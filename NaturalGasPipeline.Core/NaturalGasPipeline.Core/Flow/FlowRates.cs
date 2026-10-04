using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

/// <summary>
/// The three physically distinct representations of a gas flow rate.
/// The standard conditions are carried by <see cref="StandardVolumeFlow"/>.
/// </summary>
public class FlowRates
{
    public MassFlow MassFlow { get; set; }

    public ActualVolumeFlow ActualVolumetricFlow { get; set; }

    public StandardVolumeFlow? StandardVolumetricFlow { get; set; }
}
