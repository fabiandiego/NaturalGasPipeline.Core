using UnitsNet;

namespace NaturalGasPipeline.Core;

public class Pipe
{
    public Length OuterDiameter { get; set; }
    public Length WallThickness { get; set; }
    public Length Length { get; set; }
    public Length Roughness { get; set; }
}