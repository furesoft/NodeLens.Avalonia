using Projectional.Editing;
using Xunit;

namespace Projectional.Core.Tests;

/// <summary>An action whose behaviour the test supplies, which is how a failing preview is provoked.</summary>
internal sealed class DelegateAction(Func<EditContext, EditResult> apply) : EditAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context) => apply(context);

    public override string ToString() => "test action";
}
