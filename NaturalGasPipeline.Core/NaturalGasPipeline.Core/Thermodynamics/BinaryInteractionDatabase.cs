namespace NaturalGasPipeline.Core.Thermodynamics;

public class BinaryInteractionDatabase
{
    private readonly List<BinaryInteractionParameter> _parameters = new();

    public void Add(
        string componentA,
        string componentB,
        double value)
    {
        _parameters.Add(
            new BinaryInteractionParameter
            {
                ComponentA = componentA,
                ComponentB = componentB,
                Value = value
            });
    }

    public double Get(
        string componentA,
        string componentB)
    {
        var parameter = _parameters.FirstOrDefault(
            p =>
                (p.ComponentA == componentA &&
                 p.ComponentB == componentB)
                ||
                (p.ComponentA == componentB &&
                 p.ComponentB == componentA));

        return parameter?.Value ?? 0.0;
    }
}