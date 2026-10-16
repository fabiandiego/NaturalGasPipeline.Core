using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace NaturalGasPipeline.Mechanics.Tests;

/// <summary>
/// Keeps the "pending review" state of the Mechanics module visible: lists
/// every member marked with <see cref="PendingReviewAttribute"/> in the test
/// output of each CI run, and fails if an item disappears from the code
/// without docs/MECHANICS_REVIEW.md being updated (update
/// <see cref="ExpectedOpenItems"/> together with the checklist).
/// </summary>
public class MechanicsReviewTests
{
    private static readonly string[] ExpectedOpenItems = { "M-1", "M-2", "M-3" };

    private readonly ITestOutputHelper _output;

    public MechanicsReviewTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PendingReviewItems_AreListed()
    {
        Assembly assembly = typeof(SteelPipeDesign).Assembly;
        var found = new List<(string Item, string Member, string Reason)>();

        foreach (Type type in assembly.GetTypes())
        {
            foreach (PendingReviewAttribute a in type.GetCustomAttributes<PendingReviewAttribute>())
            {
                found.Add((a.ChecklistItem, type.Name, a.Reason));
            }

            foreach (MemberInfo m in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (PendingReviewAttribute a in m.GetCustomAttributes<PendingReviewAttribute>())
                {
                    found.Add((a.ChecklistItem, $"{type.Name}.{m.Name}", a.Reason));
                }
            }
        }

        _output.WriteLine("MECHANICS — PENDING REVIEW (see docs/MECHANICS_REVIEW.md)");

        foreach ((string item, string member, string reason) in found.OrderBy(f => f.Item))
        {
            _output.WriteLine($"  [{item}] {member}: {reason}");
        }

        Assert.Equal(ExpectedOpenItems, found.Select(f => f.Item).Distinct().OrderBy(i => i).ToArray());
    }
}
