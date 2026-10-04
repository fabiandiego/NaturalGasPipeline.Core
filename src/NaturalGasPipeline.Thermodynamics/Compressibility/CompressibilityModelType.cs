using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturalGasPipeline.Thermodynamics;

public enum CompressibilityModelType
{
    IdealGas,
    PengRobinson,
    SoaveRedlichKwong
}