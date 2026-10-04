# NaturalGasPipeline

Motor de cálculo (back end) para ingeniería de gasoductos de transmisión de gas natural.
C# / .NET 8, unidades físicas explícitas con [UnitsNet](https://github.com/angularsen/UnitsNet).
Está pensado para crecer: cada familia de modelos tiene una interfaz y se pueden añadir implementaciones sin tocar los cálculos que las usan.
El front end (Unity) se conectará más adelante.

## Estructura

```
NaturalGasPipeline.sln
Directory.Build.props        configuración común (net8.0, nullable, documentación XML)
Directory.Packages.props     versiones de NuGet, definidas una sola vez
src/
  NaturalGasPipeline.Core            sin dependencias de dominio
    Common/      PhysicalConstants (R, g)
    Flow/        caudal másico, volumétrico real y estándar, conversiones
    Gas/         GasProperties (foto inmutable de un estado)
    Numerics/    CubicEquationSolver
    Pipeline/    Pipe (DE, espesor), PipeSegment (DI, L, ε, Δz)
  NaturalGasPipeline.Thermodynamics  → Core
    Components/      GasComponent, StandardGasComponents
    Mixtures/        GasMixture, NaturalGasMixtures, parámetros de interacción binaria
    State/           GasState
    Compressibility/ ICompressibilityModel: ideal, Z constante, Peng-Robinson, SRK
    Viscosity/       IViscosityModel: Lee-Gonzalez-Eakin, constante
  NaturalGasPipeline.Hydraulics      → Core, Thermodynamics
    Friction/    IFrictionFactorModel: Colebrook-White, Haaland, Swamee-Jain, Churchill,
                 AGA totalmente turbulento, f constante, F (factor de transmisión) constante
    Reynolds, Darcy-Weisbach, IsothermalGasFlowSolver
  NaturalGasPipeline.Mechanics       → Core (reservado)
tests/
  NaturalGasPipeline.Core.Tests
  NaturalGasPipeline.Thermodynamics.Tests
  NaturalGasPipeline.Hydraulics.Tests
```

Las dependencias van en una sola dirección: `Core ← Thermodynamics ← Hydraulics`.
Los próximos módulos (Correlations, Solver, Network, Validation) seguirán la misma regla.

## Cómo añadir un modelo

1. Implementar la interfaz de su familia (`ICompressibilityModel`, `IViscosityModel`, `IFrictionFactorModel`, …) en la carpeta correspondiente.
2. Documentar en el XML la fuente, las unidades propias de la correlación y su rango de validez.
3. Convertir a las unidades de la correlación solo dentro del modelo; hacia fuera, siempre tipos de UnitsNet.
4. Añadir pruebas en dos niveles:
   - **Implementación**: el código reproduce la ecuación publicada, comparado con una referencia independiente (otra implementación o un cálculo a mano).
   - **Precisión**: el modelo comparado con datos reales o publicados (NIST, ejemplos de libro), con la banda de error medida y documentada.
5. Las constantes físicas van en `PhysicalConstants`, nunca declaradas localmente.

## Compilar y probar

Abrir `NaturalGasPipeline.sln` en Visual Studio 2022 y ejecutar todas las pruebas desde el Explorador de pruebas
(o `dotnet test` en la raíz). Las pruebas escriben sus datos intermedios con `ITestOutputHelper`.
