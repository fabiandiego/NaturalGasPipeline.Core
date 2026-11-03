using NaturalGasPipeline.Core.Common;
using UnitsNet;

namespace NaturalGasPipeline.Thermodynamics;

/// <summary>
/// AGA Report No. 8 Part 1, DETAIL characterization equation of state
/// (AGA8-DC92; ISO 12213-2) — the reference equation for the compression
/// factor of natural gas in transmission and custody transfer.
///
/// Port of the NIST reference implementation (E. W. Lemmon, I. H. Bell,
/// usnistgov/AGA8, <c>AGA8CODE/C/Detail.cpp</c>, version 2.0, 2017); the
/// NIST code is not subject to copyright in the United States and its
/// notice permits use, modification and redistribution. Constants, units
/// (kPa, mol/L, K, R = 8.31451) and algorithms are kept as in the original;
/// only the global state was replaced by an immutable, thread-safe instance
/// per composition.
///
/// Uncertainty stated by AGA for the normal range (≈ 265–335 K, ≤ 12 MPa,
/// pipeline-quality gas): about 0.1 % in Z. Outside the normal range the
/// equation is still evaluated; the caller warns. The normal range has two
/// independent parts, checked separately so one cause gives one warning:
/// pressure and temperature (<see cref="IsWithinValidatedRange"/>, called
/// at every point) and composition (<see cref="CompositionWithinNormalRange"/>,
/// a property of the gas, checked once by whoever defines the fluid).
/// </summary>
public sealed class Aga8DetailModel : ICompressibilityCorrelation
{
    /// <summary>Number of components of the DETAIL equation.</summary>
    public const int ComponentCount = 21;

    private const int NTerms = 58;
    private const double Epsilon = 1e-15;

    private readonly double[] _x = new double[ComponentCount + 1];
    private readonly double[] _bs = new double[18 + 1];
    private readonly double[] _csn = new double[NTerms + 1];
    private readonly double _k3;

    /// <summary>
    /// Builds the model for a mixture. Component names map onto the DETAIL
    /// components (see <see cref="DetailIndex"/>); an unknown name throws —
    /// no component is dropped or substituted silently. Mole fractions must
    /// add up to 1 (±1e-6).
    /// </summary>
    public Aga8DetailModel(GasMixture mixture)
    {
        Guard.NotNull(mixture);
        double total = 0.0;

        foreach (GasComponent c in mixture.Components)
        {
            int i = DetailIndex(c.Name);
            _x[i] += c.MoleFraction;
            total += c.MoleFraction;
        }

        if (Math.Abs(total - 1.0) > 1e-6)
        {
            throw new ArgumentException($"AGA8 DETAIL: mole fractions add up to {total:R}, not 1.", nameof(mixture));
        }

        _k3 = Composition(_x, _bs, _csn);
    }

    /// <summary>Builds the model from mole fractions in DETAIL order (index 1..21; index 0 unused).</summary>
    public Aga8DetailModel(IReadOnlyList<double> detailFractions)
    {
        Guard.NotNull(detailFractions);
        if (detailFractions.Count != ComponentCount + 1)
        {
            throw new ArgumentException($"Expected {ComponentCount + 1} values (index 0 unused).", nameof(detailFractions));
        }

        for (int i = 1; i <= ComponentCount; i++)
        {
            _x[i] = detailFractions[i];
        }

        _k3 = Composition(_x, _bs, _csn);
    }

    public string Name => "AGA8-DC92 (ISO 12213-2)";

    /// <summary>Mole fraction of each DETAIL component (index 1..21).</summary>
    public IReadOnlyList<double> DetailFractions => _x;

    /// <summary>
    /// Index of an engine component name in the DETAIL component list:
    /// 1 methane, 2 nitrogen, 3 CO2, 4 ethane, 5 propane, 6 isobutane,
    /// 7 n-butane, 8 isopentane, 9 n-pentane, 10 n-hexane, 11 n-heptane,
    /// 12 n-octane, 13 n-nonane, 14 n-decane, 15 hydrogen, 16 oxygen,
    /// 17 CO, 18 water, 19 H2S, 20 helium, 21 argon.
    /// </summary>
    public static int DetailIndex(string componentName) => componentName switch
    {
        "Methane" => 1,
        "Nitrogen" => 2,
        "CarbonDioxide" => 3,
        "Ethane" => 4,
        "Propane" => 5,
        "IsoButane" => 6,
        "NormalButane" => 7,
        "IsoPentane" => 8,
        "NormalPentane" => 9,
        "NormalHexane" => 10,
        "NormalHeptane" => 11,
        "NormalOctane" => 12,
        "NormalNonane" => 13,
        "NormalDecane" => 14,
        "Hydrogen" => 15,
        "Oxygen" => 16,
        "CarbonMonoxide" => 17,
        "Water" => 18,
        "HydrogenSulfide" => 19,
        "Helium" => 20,
        "Argon" => 21,
        _ => throw new ArgumentException($"AGA8 DETAIL has no component '{componentName}'.", nameof(componentName))
    };

    /// <summary>Molar mass of the mixture with the DETAIL molar masses (g/mol).</summary>
    public double MolarMassGramsPerMole
    {
        get
        {
            double mm = 0.0;
            for (int i = 1; i <= ComponentCount; i++)
            {
                mm += _x[i] * P.MolarMass[i];
            }

            return mm;
        }
    }

    public double Calculate(Pressure pressure, Temperature temperature)
    {
        double t = temperature.Kelvins;
        double pKpa = pressure.Kilopascals;
        if (!(t > 0.0) || double.IsInfinity(t))
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), t, "Temperature must be positive.");
        }

        double d = Density(t, pKpa);
        PressureAt(t, d, out _, out double z, out _);
        return z;
    }

    /// <summary>Lower temperature of the AGA 8 (1994) normal range: 17 °F = 264.8167 K (exact °F → K).</summary>
    public const double NormalRangeMinimumTemperatureKelvins = (17.0 + 459.67) * 5.0 / 9.0;

    /// <summary>Upper temperature of the AGA 8 (1994) normal range: 143 °F = 334.8167 K (exact °F → K).</summary>
    public const double NormalRangeMaximumTemperatureKelvins = (143.0 + 459.67) * 5.0 / 9.0;

    /// <summary>
    /// Upper pressure of the AGA 8 (1994) normal range: 1750 psia = 12.0658 MPa.
    /// 1 psi = 0.45359237 kg · 9.80665 m/s² / (0.0254 m)², from the exact definitions
    /// of the pound, standard gravity and the inch.
    /// </summary>
    public const double NormalRangeMaximumPressurePascals = 1750.0 * 0.45359237 * 9.80665 / (0.0254 * 0.0254);

    /// <summary>
    /// Pressure–temperature part of the AGA 8 (1994) normal range for the
    /// DETAIL method: 17–143 °F, up to 1750 psia (see the NormalRange
    /// constants). Honours <see cref="ICompressibilityCorrelation"/>: only
    /// (P, T) is checked. The composition part of the normal range is
    /// <see cref="CompositionWithinNormalRange"/>.
    /// </summary>
    public bool IsWithinValidatedRange(Pressure pressure, Temperature temperature) =>
        temperature.Kelvins >= NormalRangeMinimumTemperatureKelvins
        && temperature.Kelvins <= NormalRangeMaximumTemperatureKelvins
        && pressure.Pascals <= NormalRangeMaximumPressurePascals;

    /// <summary>
    /// Composition part of the AGA 8 (1994) normal range; lists every limit
    /// exceeded. Limits (mol %): CH4 ≥ 45, N2 ≤ 50, CO2 ≤ 30, C2H6 ≤ 10,
    /// C3H8 ≤ 4, butanes ≤ 1, pentanes ≤ 0.3, C6+ ≤ 0.2, H2 ≤ 10, O2 = 0,
    /// CO ≤ 3, H2O ≤ 0.05, H2S ≤ 0.02, He ≤ 0.2, Ar = 0.
    /// Independent of P and T: a gas outside these limits keeps
    /// <see cref="IsWithinValidatedRange"/> true at in-range (P, T), but the
    /// stated 0.1 % uncertainty in Z does not apply to it.
    /// </summary>
    public bool CompositionWithinNormalRange(out IReadOnlyList<string> violations)
    {
        var v = new List<string>();
        void Max(string name, double fraction, double maxPercent)
        {
            if (fraction * 100.0 > maxPercent + 1e-9)
            {
                v.Add($"{name} {fraction * 100.0:0.###} % > {maxPercent} %");
            }
        }

        if (_x[1] * 100.0 < 45.0 - 1e-9)
        {
            v.Add($"methane {_x[1] * 100.0:0.###} % < 45 %");
        }

        Max("nitrogen", _x[2], 50.0);
        Max("CO2", _x[3], 30.0);
        Max("ethane", _x[4], 10.0);
        Max("propane", _x[5], 4.0);
        Max("butanes", _x[6] + _x[7], 1.0);
        Max("pentanes", _x[8] + _x[9], 0.3);
        Max("hexanes+", _x[10] + _x[11] + _x[12] + _x[13] + _x[14], 0.2);
        Max("hydrogen", _x[15], 10.0);
        Max("oxygen", _x[16], 0.0);
        Max("CO", _x[17], 3.0);
        Max("water", _x[18], 0.05);
        Max("H2S", _x[19], 0.02);
        Max("helium", _x[20], 0.2);
        Max("argon", _x[21], 0.0);

        violations = v;
        return v.Count == 0;
    }

    /// <summary>Molar density (mol/L) at T (K) and P (kPa): the NIST iteration on ln(v).</summary>
    public double Density(double temperatureKelvins, double pressureKilopascals)
    {
        double t = temperatureKelvins;
        double p = pressureKilopascals;
        if (Math.Abs(p) < Epsilon)
        {
            return 0.0;
        }

        const double tolr = 0.0000001;
        double d = p / P.R / t;
        double plog = Math.Log(p);
        double vlog = -Math.Log(d);

        for (int it = 1; it <= 20; it++)
        {
            if (vlog < -7 || vlog > 100)
            {
                break;
            }

            d = Math.Exp(-vlog);
            PressureAt(t, d, out double p2, out _, out double dPdD);
            if (dPdD < Epsilon || p2 < Epsilon)
            {
                vlog += 0.1;
            }
            else
            {
                double dpdlv = -d * dPdD;
                double vdiff = (Math.Log(p2) - plog) * p2 / dpdlv;
                vlog -= vdiff;
                if (Math.Abs(vdiff) < tolr)
                {
                    return Math.Exp(-vlog);
                }
            }
        }

        // The NIST code returns the ideal-gas density here; failing loudly avoids a silent wrong Z.
        throw new InvalidOperationException(
            $"AGA8 DETAIL density did not converge at T = {t} K, P = {p} kPa (two-phase or outside the equation's range).");
    }

    /// <summary>Pressure (kPa), Z and dP/dD [kPa/(mol/L)] at T (K) and molar density D (mol/L).</summary>
    public void PressureAt(double temperatureKelvins, double densityMolPerLitre, out double pressureKilopascals, out double z, out double dPdD)
    {
        double t = temperatureKelvins;
        double d = densityMolPerLitre;
        double[,] ar = Alphar(false, t, d);
        z = 1 + ar[0, 1] / P.R / t;
        pressureKilopascals = d * P.R * t * z;
        dPdD = P.R * t + 2 * ar[0, 1] + ar[0, 2];
    }

    /// <summary>Thermodynamic properties at T (K) and P (kPa), as in the NIST PropertiesDetail.</summary>
    public Aga8Properties Properties(double temperatureKelvins, double pressureKilopascals)
    {
        double t = temperatureKelvins;
        double d = Density(t, pressureKilopascals);
        double mm = MolarMassGramsPerMole;
        double[] a0 = Alpha0(t, d);
        double[,] ar = Alphar(true, t, d);

        double r = P.R;
        double rt = r * t;
        double z = 1 + ar[0, 1] / rt;
        double p = d * rt * z;
        double dPdD = rt + 2 * ar[0, 1] + ar[0, 2];
        double dPdT = d * r + d * ar[1, 1];
        double a = a0[0] + ar[0, 0];
        double s = -a0[1] - ar[1, 0];
        double u = a + t * s;
        double cv = -(a0[2] + ar[2, 0]);
        double h, g, cp, d2PdD2, jt;

        if (d > Epsilon)
        {
            h = u + p / d;
            g = a + p / d;
            cp = cv + t * (dPdT / d) * (dPdT / d) / dPdD;
            d2PdD2 = (2 * ar[0, 1] + 4 * ar[0, 2] + ar[0, 3]) / d;
            jt = (t / d * dPdT / dPdD - 1) / cp / d;
        }
        else
        {
            h = u + rt;
            g = a + rt;
            cp = cv + r;
            d2PdD2 = 0;
            jt = 1e20;
        }

        double w = 1000 * cp / cv * dPdD / mm;
        if (w < 0)
        {
            w = 0;
        }

        w = Math.Sqrt(w);
        double kappa = w * w * mm / (rt * 1000 * z);

        return new Aga8Properties(mm, d, p, z, dPdD, d2PdD2, dPdT, u, h, s, cv, cp, w, g, jt, kappa);
    }

    // ------------------------------------------------------------------ composition terms (xTermsDetail)

    private static double Composition(double[] x, double[] bs, double[] csn)
    {
        double k3 = 0, u = 0, g = 0, q = 0, f = 0;

        for (int i = 1; i <= ComponentCount; i++)
        {
            if (x[i] > 0)
            {
                double xi2 = x[i] * x[i];
                k3 += x[i] * P.Ki25[i];
                u += x[i] * P.Ei25[i];
                g += x[i] * P.Gi[i];
                q += x[i] * P.Qi[i];
                f += xi2 * P.Fi[i];
                for (int n = 1; n <= 18; n++)
                {
                    bs[n] += xi2 * P.Bsnij2[i, i, n];
                }
            }
        }

        k3 *= k3;
        u *= u;

        for (int i = 1; i <= ComponentCount - 1; i++)
        {
            if (x[i] > 0)
            {
                for (int j = i + 1; j <= ComponentCount; j++)
                {
                    if (x[j] > 0)
                    {
                        double xij = 2 * x[i] * x[j];
                        k3 += xij * P.Kij5[i, j];
                        u += xij * P.Uij5[i, j];
                        g += xij * P.Gij5[i, j];
                        for (int n = 1; n <= 18; n++)
                        {
                            bs[n] += xij * P.Bsnij2[i, j, n];
                        }
                    }
                }
            }
        }

        k3 = Math.Pow(k3, 0.6);
        u = Math.Pow(u, 0.2);

        double q2 = q * q;
        for (int n = 13; n <= 58; n++)
        {
            csn[n] = P.An[n] * Math.Pow(u, P.Un[n]);
            if (P.Gn[n] == 1)
            {
                csn[n] *= g;
            }

            if (P.Qn[n] == 1)
            {
                csn[n] *= q2;
            }

            if (P.Fn[n] == 1)
            {
                csn[n] *= f;
            }
        }

        return k3;
    }

    // ------------------------------------------------------------------ Helmholtz energy

    private double[] Alpha0(double t, double d)
    {
        var a0 = new double[3];
        double logD = d > Epsilon ? Math.Log(d) : Math.Log(Epsilon);
        double logT = Math.Log(t);

        for (int i = 1; i <= ComponentCount; i++)
        {
            if (_x[i] > 0)
            {
                double logxD = logD + Math.Log(_x[i]);
                double sumHyp0 = 0, sumHyp1 = 0, sumHyp2 = 0;

                for (int j = 4; j <= 7; j++)
                {
                    if (P.Th0i[i, j] > 0)
                    {
                        double th0T = P.Th0i[i, j] / t;
                        double ep = Math.Exp(th0T);
                        double em = 1 / ep;
                        double hsn = (ep - em) / 2;
                        double hcn = (ep + em) / 2;
                        if (j == 4 || j == 6)
                        {
                            double logHyp = Math.Log(Math.Abs(hsn));
                            sumHyp0 += P.N0i[i, j] * logHyp;
                            sumHyp1 += P.N0i[i, j] * (logHyp - th0T * hcn / hsn);
                            sumHyp2 += P.N0i[i, j] * (th0T / hsn) * (th0T / hsn);
                        }
                        else
                        {
                            double logHyp = Math.Log(Math.Abs(hcn));
                            sumHyp0 += -P.N0i[i, j] * logHyp;
                            sumHyp1 += -P.N0i[i, j] * (logHyp - th0T * hsn / hcn);
                            sumHyp2 += +P.N0i[i, j] * (th0T / hcn) * (th0T / hcn);
                        }
                    }
                }

                a0[0] += _x[i] * (logxD + P.N0i[i, 1] + P.N0i[i, 2] / t - P.N0i[i, 3] * logT + sumHyp0);
                a0[1] += _x[i] * (logxD + P.N0i[i, 1] - P.N0i[i, 3] * (1 + logT) + sumHyp1);
                a0[2] += -_x[i] * (P.N0i[i, 3] + sumHyp2);
            }
        }

        a0[0] *= P.R * t;
        a0[1] *= P.R;
        a0[2] *= P.R;
        return a0;
    }

    private double[,] Alphar(bool temperatureDerivatives, double t, double d)
    {
        var ar = new double[4, 4];
        var tun = new double[NTerms + 1];
        for (int n = 1; n <= NTerms; n++)
        {
            tun[n] = Math.Pow(t, -P.Un[n]);
        }

        double dred = _k3 * d;
        var dknn = new double[10];
        dknn[0] = 1;
        for (int n = 1; n <= 9; n++)
        {
            dknn[n] = dred * dknn[n - 1];
        }

        var expn = new double[5];
        expn[0] = 1;
        for (int n = 1; n <= 4; n++)
        {
            expn[n] = Math.Exp(-dknn[n]);
        }

        double rt = P.R * t;

        for (int n = 1; n <= NTerms; n++)
        {
            double coefT1 = P.R * (P.Un[n] - 1);
            double coefT2 = coefT1 * P.Un[n];
            double sumB = 0, sum0 = 0, coefD1 = 0, coefD2 = 0, coefD3 = 0;

            if (n <= 18)
            {
                double sum = _bs[n] * d;
                if (n >= 13)
                {
                    sum += -_csn[n] * dred;
                }

                sumB = sum * tun[n];
            }

            if (n >= 13)
            {
                int bn = P.Bn[n];
                int kn = P.Kn[n];
                sum0 = _csn[n] * dknn[bn] * tun[n] * expn[kn];
                double bkd = bn - kn * dknn[kn];
                double ckd = kn * kn * dknn[kn];
                coefD1 = bkd;
                coefD2 = bkd * (bkd - 1) - ckd;
                coefD3 = (bkd - 2) * coefD2 + ckd * (1 - kn - 2 * bkd);
            }

            double s0 = sum0 + sumB;
            double s1 = sum0 * coefD1 + sumB;
            double s2 = sum0 * coefD2;
            double s3 = sum0 * coefD3;
            ar[0, 0] += rt * s0;
            ar[0, 1] += rt * s1;
            ar[0, 2] += rt * s2;
            ar[0, 3] += rt * s3;

            if (temperatureDerivatives)
            {
                ar[1, 0] -= coefT1 * s0;
                ar[1, 1] -= coefT1 * s1;
                ar[2, 0] += coefT2 * s0;
            }
        }

        return ar;
    }

    /// <summary>Constants of the DETAIL equation (SetupDetail), computed once.</summary>
    private static class P
    {
        public const double R = 8.31451;

        public static readonly double[] MolarMass =
        {
            0, 16.043, 28.0135, 44.01, 30.07, 44.097, 58.123, 58.123, 72.15, 72.15, 86.177,
            100.204, 114.231, 128.258, 142.285, 2.0159, 31.9988, 28.01, 18.0153, 34.082, 4.0026, 39.948
        };

        public static readonly double[] An =
        {
            0,
            0.1538326, 1.341953, -2.998583, -0.04831228, 0.3757965, -1.589575, -0.05358847, 0.88659463, -0.71023704, -1.471722,
            1.32185035, -0.78665925, 0.00000000229129, 0.1576724, -0.4363864, -0.04408159, -0.003433888, 0.03205905, 0.02487355, 0.07332279,
            -0.001600573, 0.6424706, -0.4162601, -0.06689957, 0.2791795, -0.6966051, -0.002860589, -0.008098836, 3.150547, 0.007224479,
            -0.7057529, 0.5349792, -0.07931491, -1.418465, -5.99905E-17, 0.1058402, 0.03431729, -0.007022847, 0.02495587, 0.04296818,
            0.7465453, -0.2919613, 7.294616, -9.936757, -0.005399808, -0.2432567, 0.04987016, 0.003733797, 1.874951, 0.002168144,
            -0.6587164, 0.000205518, 0.009776195, -0.02048708, 0.01557322, 0.006862415, -0.001226752, 0.002850908
        };

        public static readonly int[] Bn =
        {
            0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 3, 3, 3,
            3, 3, 3, 3, 3, 3, 3, 4, 4, 4,
            4, 4, 4, 4, 5, 5, 5, 5, 5, 6,
            6, 7, 7, 8, 8, 8, 9, 9
        };

        public static readonly int[] Kn = BuildKn();

        public static readonly double[] Un =
        {
            0,
            0, 0.5, 1, 3.5, -0.5, 4.5, 0.5, 7.5, 9.5, 6,
            12, 12.5, -6, 2, 3, 2, 2, 11, -0.5, 0.5,
            0, 4, 6, 21, 23, 22, -1, -0.5, 7, -1,
            6, 4, 1, 9, -13, 21, 8, -0.5, 0, 2,
            7, 9, 22, 23, 1, 9, 3, 8, 23, 1.5,
            5, -0.5, 4, 7, 3, 0, 1, 0
        };

        public static readonly int[] Fn = Flags(13, 27, 30, 35);
        public static readonly int[] Gn = Flags(5, 6, 25, 29, 32, 33, 34, 51, 54, 56);
        public static readonly int[] Qn = Flags(7, 16, 26, 28, 37, 42, 47, 49, 52, 58);
        private static readonly int[] Sn = Flags(8, 9);
        private static readonly int[] Wn = Flags(10, 11, 12);

        private static readonly double[] Ei =
        {
            0, 151.3183, 99.73778, 241.9606, 244.1667, 298.1183, 324.0689, 337.6389, 365.5999, 370.6823, 402.636293,
            427.72263, 450.325022, 470.840891, 489.558373, 26.95794, 122.7667, 105.5348, 514.0156, 296.355, 2.610111, 119.6299
        };

        private static readonly double[] Ki =
        {
            0, 0.4619255, 0.4479153, 0.4557489, 0.5279209, 0.583749, 0.6406937, 0.6341423, 0.6738577, 0.6798307, 0.7175118,
            0.7525189, 0.784955, 0.8152731, 0.8437826, 0.3514916, 0.4186954, 0.4533894, 0.3825868, 0.4618263, 0.3589888, 0.4216551
        };

        public static readonly double[] Gi =
        {
            0, 0, 0.027815, 0.189065, 0.0793, 0.141239, 0.256692, 0.281835, 0.332267, 0.366911, 0.289731,
            0.337542, 0.383381, 0.427354, 0.469659, 0.034369, 0.021, 0.038953, 0.3325, 0.0885, 0, 0
        };

        public static readonly double[] Qi = Sparse(new Dictionary<int, double> { [3] = 0.69, [18] = 1.06775, [19] = 0.633276 });
        public static readonly double[] Fi = Sparse(new Dictionary<int, double> { [15] = 1 });
        private static readonly double[] Si = Sparse(new Dictionary<int, double> { [18] = 1.5822, [19] = 0.39 });
        private static readonly double[] Wi = Sparse(new Dictionary<int, double> { [18] = 1 });

        public static readonly double[] Ki25 = new double[ComponentCount + 1];
        public static readonly double[] Ei25 = new double[ComponentCount + 1];
        public static readonly double[,,] Bsnij2 = new double[ComponentCount + 1, ComponentCount + 1, 18 + 1];
        public static readonly double[,] Kij5 = new double[ComponentCount + 1, ComponentCount + 1];
        public static readonly double[,] Uij5 = new double[ComponentCount + 1, ComponentCount + 1];
        public static readonly double[,] Gij5 = new double[ComponentCount + 1, ComponentCount + 1];
        public static readonly double[,] N0i = new double[ComponentCount + 1, 8];
        public static readonly double[,] Th0i = new double[ComponentCount + 1, 8];

        static P()
        {
            var eij = Ones();
            var uij = Ones();
            var kij = Ones();
            var gij = Ones();

            Set(eij, (1, 2, 0.97164), (1, 3, 0.960644), (1, 5, 0.994635), (1, 6, 1.01953), (1, 7, 0.989844), (1, 8, 1.00235),
                (1, 9, 0.999268), (1, 10, 1.107274), (1, 11, 0.88088), (1, 12, 0.880973), (1, 13, 0.881067), (1, 14, 0.881161),
                (1, 15, 1.17052), (1, 17, 0.990126), (1, 18, 0.708218), (1, 19, 0.931484),
                (2, 3, 1.02274), (2, 4, 0.97012), (2, 5, 0.945939), (2, 6, 0.946914), (2, 7, 0.973384), (2, 8, 0.95934),
                (2, 9, 0.94552), (2, 15, 1.08632), (2, 16, 1.021), (2, 17, 1.00571), (2, 18, 0.746954), (2, 19, 0.902271),
                (3, 4, 0.925053), (3, 5, 0.960237), (3, 6, 0.906849), (3, 7, 0.897362), (3, 8, 0.726255), (3, 9, 0.859764),
                (3, 10, 0.855134), (3, 11, 0.831229), (3, 12, 0.80831), (3, 13, 0.786323), (3, 14, 0.765171), (3, 15, 1.28179),
                (3, 17, 1.5), (3, 18, 0.849408), (3, 19, 0.955052),
                (4, 5, 1.02256), (4, 7, 1.01306), (4, 9, 1.00532), (4, 15, 1.16446), (4, 18, 0.693168), (4, 19, 0.946871),
                (5, 7, 1.0049), (5, 15, 1.034787), (6, 15, 1.3), (7, 15, 1.3),
                (10, 19, 1.008692), (11, 19, 1.010126), (12, 19, 1.011501), (13, 19, 1.012821), (14, 19, 1.014089), (15, 17, 1.1));

            Set(uij, (1, 2, 0.886106), (1, 3, 0.963827), (1, 5, 0.990877), (1, 7, 0.992291), (1, 9, 1.00367), (1, 10, 1.302576),
                (1, 11, 1.191904), (1, 12, 1.205769), (1, 13, 1.219634), (1, 14, 1.233498), (1, 15, 1.15639), (1, 19, 0.736833),
                (2, 3, 0.835058), (2, 4, 0.816431), (2, 5, 0.915502), (2, 7, 0.993556), (2, 15, 0.408838), (2, 19, 0.993476),
                (3, 4, 0.96987), (3, 10, 1.066638), (3, 11, 1.077634), (3, 12, 1.088178), (3, 13, 1.098291), (3, 14, 1.108021),
                (3, 17, 0.9), (3, 19, 1.04529),
                (4, 5, 1.065173), (4, 6, 1.25), (4, 7, 1.25), (4, 8, 1.25), (4, 9, 1.25), (4, 15, 1.61666), (4, 19, 0.971926),
                (10, 19, 1.028973), (11, 19, 1.033754), (12, 19, 1.038338), (13, 19, 1.042735), (14, 19, 1.046966));

            Set(kij, (1, 2, 1.00363), (1, 3, 0.995933), (1, 5, 1.007619), (1, 7, 0.997596), (1, 9, 1.002529), (1, 10, 0.982962),
                (1, 11, 0.983565), (1, 12, 0.982707), (1, 13, 0.981849), (1, 14, 0.980991), (1, 15, 1.02326), (1, 19, 1.00008),
                (2, 3, 0.982361), (2, 4, 1.00796), (2, 15, 1.03227), (2, 19, 0.942596),
                (3, 4, 1.00851), (3, 10, 0.910183), (3, 11, 0.895362), (3, 12, 0.881152), (3, 13, 0.86752), (3, 14, 0.854406),
                (3, 19, 1.00779),
                (4, 5, 0.986893), (4, 15, 1.02034), (4, 19, 0.999969),
                (10, 19, 0.96813), (11, 19, 0.96287), (12, 19, 0.957828), (13, 19, 0.952441), (14, 19, 0.948338));

            Set(gij, (1, 3, 0.807653), (1, 15, 1.95731), (2, 3, 0.982746), (3, 4, 0.370296), (3, 18, 1.67309));

            // Ideal-gas parameters: n0i[i][1..7], th0i[i][4..7]
            double[][] n0 =
            {
                new double[] { 0 },
                new[] { 0, 29.83843397, -15999.69151, 4.00088, 0.76315, 0.0046, 8.74432, -4.46921 },
                new[] { 0, 17.56770785, -2801.729072, 3.50031, 0.13732, -0.1466, 0.90066, 0 },
                new[] { 0, 20.65844696, -4902.171516, 3.50002, 2.04452, -1.06044, 2.03366, 0.01393 },
                new[] { 0, 36.73005938, -23639.65301, 4.00263, 4.33939, 1.23722, 13.1974, -6.01989 },
                new[] { 0, 44.70909619, -31236.63551, 4.02939, 6.60569, 3.197, 19.1921, -8.37267 },
                new[] { 0, 34.30180349, -38525.50276, 4.06714, 8.97575, 5.25156, 25.1423, 16.1388 },
                new[] { 0, 36.53237783, -38957.80933, 4.33944, 9.44893, 6.89406, 24.4618, 14.7824 },
                new[] { 0, 43.17218626, -51198.30946, 4, 11.7618, 20.1101, 33.1688, 0 },
                new[] { 0, 42.67837089, -45215.83, 4, 8.95043, 21.836, 33.4032, 0 },
                new[] { 0, 46.99717188, -52746.83318, 4, 11.6977, 26.8142, 38.6164, 0 },
                new[] { 0, 52.07631631, -57104.81056, 4, 13.7266, 30.4707, 43.5561, 0 },
                new[] { 0, 57.25830934, -60546.76385, 4, 15.6865, 33.8029, 48.1731, 0 },
                new[] { 0, 62.09646901, -66600.12837, 4, 18.0241, 38.1235, 53.3415, 0 },
                new[] { 0, 65.93909154, -74131.45483, 4, 21.0069, 43.4931, 58.3657, 0 },
                new[] { 0, 13.07520288, -5836.943696, 2.47906, 0.95806, 0.45444, 1.56039, -1.3756 },
                new[] { 0, 16.8017173, -2318.32269, 3.50146, 1.07558, 1.01334, 0, 0 },
                new[] { 0, 17.45786899, -2635.244116, 3.50055, 1.02865, 0.00493, 0, 0 },
                new[] { 0, 21.57882705, -7766.733078, 4.00392, 0.01059, 0.98763, 3.06904, 0 },
                new[] { 0, 21.5830944, -6069.035869, 4, 3.11942, 1.00243, 0, 0 },
                new[] { 0, 10.04639507, -745.375, 2.5, 0, 0, 0, 0 },
                new[] { 0, 10.04639507, -745.375, 2.5, 0, 0, 0, 0 }
            };

            double[][] th0 =
            {
                new double[] { 0 },
                new[] { 820.659, 178.41, 1062.82, 1090.53 },
                new[] { 662.738, 680.562, 1740.06, 0 },
                new[] { 919.306, 865.07, 483.553, 341.109 },
                new[] { 559.314, 223.284, 1031.38, 1071.29 },
                new[] { 479.856, 200.893, 955.312, 1027.29 },
                new[] { 438.27, 198.018, 1905.02, 893.765 },
                new[] { 468.27, 183.636, 1914.1, 903.185 },
                new[] { 292.503, 910.237, 1919.37, 0 },
                new[] { 178.67, 840.538, 1774.25, 0 },
                new[] { 182.326, 859.207, 1826.59, 0 },
                new[] { 169.789, 836.195, 1760.46, 0 },
                new[] { 158.922, 815.064, 1693.07, 0 },
                new[] { 156.854, 814.882, 1693.79, 0 },
                new[] { 164.947, 836.264, 1750.24, 0 },
                new[] { 228.734, 326.843, 1651.71, 1671.69 },
                new[] { 2235.71, 1116.69, 0, 0 },
                new[] { 1550.45, 704.525, 0, 0 },
                new[] { 268.795, 1141.41, 2507.37, 0 },
                new[] { 1833.63, 847.181, 0, 0 },
                new[] { 0.0, 0, 0, 0 },
                new[] { 0.0, 0, 0, 0 }
            };

            for (int i = 1; i <= ComponentCount; i++)
            {
                for (int k = 1; k <= 7; k++)
                {
                    N0i[i, k] = n0[i][k];
                }

                for (int k = 4; k <= 7; k++)
                {
                    Th0i[i, k] = th0[i][k - 4];
                }
            }

            for (int i = 1; i <= ComponentCount; i++)
            {
                Ki25[i] = Math.Pow(Ki[i], 2.5);
                Ei25[i] = Math.Pow(Ei[i], 2.5);
            }

            for (int i = 1; i <= ComponentCount; i++)
            {
                for (int j = i; j <= ComponentCount; j++)
                {
                    for (int n = 1; n <= 18; n++)
                    {
                        double bsnij = 1;
                        if (Gn[n] == 1)
                        {
                            bsnij = gij[i, j] * (Gi[i] + Gi[j]) / 2;
                        }

                        if (Qn[n] == 1)
                        {
                            bsnij *= Qi[i] * Qi[j];
                        }

                        if (Fn[n] == 1)
                        {
                            bsnij *= Fi[i] * Fi[j];
                        }

                        if (Sn[n] == 1)
                        {
                            bsnij *= Si[i] * Si[j];
                        }

                        if (Wn[n] == 1)
                        {
                            bsnij *= Wi[i] * Wi[j];
                        }

                        Bsnij2[i, j, n] = An[n] * Math.Pow(eij[i, j] * Math.Sqrt(Ei[i] * Ei[j]), Un[n]) * Math.Pow(Ki[i] * Ki[j], 1.5) * bsnij;
                    }

                    Kij5[i, j] = (Math.Pow(kij[i, j], 5) - 1) * Ki25[i] * Ki25[j];
                    Uij5[i, j] = (Math.Pow(uij[i, j], 5) - 1) * Ei25[i] * Ei25[j];
                    Gij5[i, j] = (gij[i, j] - 1) * (Gi[i] + Gi[j]) / 2;
                }
            }

            double d0 = 101.325 / R / 298.15;
            for (int i = 1; i <= ComponentCount; i++)
            {
                N0i[i, 3] -= 1;
                N0i[i, 1] -= Math.Log(d0);
            }
        }

        private static int[] BuildKn()
        {
            var kn = new int[NTerms + 1];
            (int n, int k)[] values =
            {
                (13, 3), (14, 2), (15, 2), (16, 2), (17, 4), (18, 4), (21, 2), (22, 2), (23, 2), (24, 4),
                (25, 4), (26, 4), (27, 4), (29, 1), (30, 1), (31, 2), (32, 2), (33, 3), (34, 3), (35, 4),
                (36, 4), (37, 4), (40, 2), (41, 2), (42, 2), (43, 4), (44, 4), (46, 2), (47, 2), (48, 4),
                (49, 4), (51, 2), (53, 2), (54, 1), (55, 2), (56, 2), (57, 2), (58, 2)
            };
            foreach ((int n, int k) in values)
            {
                kn[n] = k;
            }

            return kn;
        }

        private static int[] Flags(params int[] indices)
        {
            var f = new int[NTerms + 1];
            foreach (int i in indices)
            {
                f[i] = 1;
            }

            return f;
        }

        private static double[] Sparse(Dictionary<int, double> values)
        {
            var a = new double[ComponentCount + 1];
            foreach (KeyValuePair<int, double> kv in values)
            {
                a[kv.Key] = kv.Value;
            }

            return a;
        }

        private static double[,] Ones()
        {
            var a = new double[ComponentCount + 1, ComponentCount + 1];
            for (int i = 0; i <= ComponentCount; i++)
            {
                for (int j = 0; j <= ComponentCount; j++)
                {
                    a[i, j] = 1;
                }
            }

            return a;
        }

        private static void Set(double[,] a, params (int i, int j, double v)[] values)
        {
            foreach ((int i, int j, double v) in values)
            {
                a[i, j] = v;
            }
        }
    }
}

/// <summary>Properties from the AGA8 DETAIL equation (units of the NIST code: g/mol, mol/L, kPa, J/mol, K).</summary>
public sealed record Aga8Properties(
    double MolarMassGramsPerMole,
    double DensityMolPerLitre,
    double PressureKilopascals,
    double Z,
    double DPdD,
    double D2PdD2,
    double DPdT,
    double InternalEnergy,
    double Enthalpy,
    double Entropy,
    double Cv,
    double Cp,
    double SpeedOfSound,
    double GibbsEnergy,
    double JouleThomson,
    double IsentropicExponent);
