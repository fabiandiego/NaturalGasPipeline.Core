using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnitsNet;

namespace NaturalGasPipeline.Core.Flow;

public class StandardFlowConditions
{
    public Temperature Temperature { get; set; }

    public Pressure Pressure { get; set; }

    public double CompressibilityFactor { get; set; }
}