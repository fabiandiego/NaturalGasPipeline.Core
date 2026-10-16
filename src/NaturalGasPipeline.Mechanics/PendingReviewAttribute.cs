namespace NaturalGasPipeline.Mechanics;

/// <summary>
/// Marks code whose engineering basis (code factors, material data or
/// formulas) has not yet been reviewed by the project owner against the
/// governing standard. Every use carries the reason and the item of
/// docs/MECHANICS_REVIEW.md that clears it.
///
/// Remove the attribute only when that checklist item is signed off.
/// The test <c>MechanicsReviewTests.PendingReviewItems_AreListed</c> prints
/// every marked member so the open items are visible in each CI run.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum |
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = true,
    Inherited = false)]
public sealed class PendingReviewAttribute : Attribute
{
    public PendingReviewAttribute(string checklistItem, string reason)
    {
        ChecklistItem = checklistItem;
        Reason = reason;
    }

    /// <summary>Item id in docs/MECHANICS_REVIEW.md, e.g. "M-2".</summary>
    public string ChecklistItem { get; }

    /// <summary>What has to be verified, and against which source.</summary>
    public string Reason { get; }
}
