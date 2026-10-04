using NaturalGasPipeline.Core.Common;
using Xunit;

namespace NaturalGasPipeline.Core.Tests;

public class GuardTests
{
    private enum Sample { A, B }

    [Fact]
    public void NotNull_ShouldReportTheArgumentExpression()
    {
        string? missing = null;

        var e = Assert.Throws<ArgumentNullException>(() => Guard.NotNull(missing));

        Assert.Equal("missing", e.ParamName);
        Guard.NotNull("present");
    }

    [Fact]
    public void IsDefined_ShouldMatchEnumMembers()
    {
        Assert.True(Guard.IsDefined(Sample.B));
        Assert.False(Guard.IsDefined((Sample)7));
    }
}
