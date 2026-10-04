using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NaturalGasPipeline.Core.Common;

/// <summary>
/// Argument checks available on every target framework. Replaces
/// ArgumentNullException.ThrowIfNull and Enum.IsDefined&lt;T&gt;, which do not
/// exist in netstandard2.1 (the framework Unity can load).
/// </summary>
public static class Guard
{
    /// <summary>Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is null.</summary>
    public static void NotNull(
        [NotNull] object? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    /// <summary>True when <paramref name="value"/> is a named member of its enum.</summary>
    public static bool IsDefined<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Enum.IsDefined(typeof(TEnum), value);
}
