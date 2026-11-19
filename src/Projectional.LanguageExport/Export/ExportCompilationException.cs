namespace Projectional.LanguageExport;

/// <summary>Raised when the generated source does not compile, which means the export wrote something C# refuses.</summary>
/// <remarks>
/// The generated file is machine-written, so a compilation failure is a defect in the export rather than something a
/// user did - but it must still reach the caller as a named outcome with the diagnostics, because "the export cannot
/// express this language yet" is exactly what a language author has to be told.
/// </remarks>
public sealed class ExportCompilationException : Exception
{
    /// <summary>The errors the compiler reported, in the compiler's own words.</summary>
    public IReadOnlyList<string> Diagnostics { get; } = [];

    public ExportCompilationException()
    {
    }

    public ExportCompilationException(string message)
        : base(message)
    {
    }

    public ExportCompilationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ExportCompilationException(string message, IReadOnlyList<string> diagnostics)
        : base(message) => Diagnostics = diagnostics;
}
