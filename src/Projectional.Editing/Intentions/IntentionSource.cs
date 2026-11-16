namespace Projectional.Editing;

/// <summary>Where an offer in the context list came from, which is also the order the list uses.</summary>
/// <remarks>
/// <para>
/// Two origins, and the declaration order <em>is</em> the resolution order: a host's entries come first, then
/// the language's, and the quick fixes are appended after both by <see cref="IntentionList"/>. Order is part
/// of the visible behaviour - the same reason <see cref="EditorMenu"/> fixes the order of its entries - so it
/// is written down here rather than left to whichever provider the host happened to register first.
/// </para>
/// <para>
/// A host's entry leads because the host's user installed that host: "deploy", "generate now", "open the
/// referenced model" are the actions somebody chose the application for. A language's entry follows because
/// it is what a user of that language expects from any editor of it. A fix is last because it is about a
/// problem, and the problem is reported on its own line before the repairs are listed.
/// </para>
/// </remarks>
public enum IntentionSource
{
    /// <summary>Registered by the embedding application: commands only that host can know about.</summary>
    Host,

    /// <summary>Declared beside a language's concepts: what is meaningful for a concept.</summary>
    Language,
}
