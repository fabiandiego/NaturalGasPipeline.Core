public class GeneralFlowEquation
{
    // Inputs – all must be in consistent units
    public double K { get; set; }   // unit conversion constant
    public double G { get; set; }   // gas specific gravity
    public double T { get; set; }   // average flowing temperature (absolute)
    public double L { get; set; }   // length
    public double D { get; set; }   // internal diameter
    public double Z { get; set; }   // compressibility factor
    public double f { get; set; }   // friction factor

    public double CalculateFlowRate(double p1, double p2)
    {
        // TODO: implement Q = ...
        // Validate p1 > p2
        throw new System.NotImplementedException();
    }

    public double CalculateInletPressure(double p2, double q)
    {
        // TODO: implement P1 = ...
        throw new System.NotImplementedException();
    }

    public double CalculateOutletPressure(double p1, double q)
    {
        // TODO: implement P2 = ...
        // Validate that the square root argument is >= 0
        throw new System.NotImplementedException();
    }
}