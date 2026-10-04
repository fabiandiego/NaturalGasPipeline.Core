using GasPipeline.Core.Equations;

var eq = new GeneralFlowEquation
{
    K = /* your conversion constant */,
    G = /* specific gravity */,
    T = /* absolute temperature */,
    L = /* length */,
    D = /* diameter */,
    Z = /* compressibility */,
    f = /* friction factor */
};

double p1 = /* inlet pressure */;
double p2 = /* outlet pressure */;

double q = eq.CalculateFlowRate(p1, p2);
Console.WriteLine($"Q = {q}");

Console.ReadKey();