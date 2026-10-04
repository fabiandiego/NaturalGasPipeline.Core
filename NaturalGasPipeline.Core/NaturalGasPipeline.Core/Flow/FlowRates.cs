using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

public class FlowRates
{
    public MassFlow MassFlow { get; set; }

    public VolumeFlow ActualVolumetricFlow { get; set; }

    public VolumeFlow StandardVolumetricFlow { get; set; }

    public Temperature StandardTemperature { get; set; }

    public Pressure StandardPressure { get; set; }

    public double StandardCompressibilityFactor { get; set; }
}