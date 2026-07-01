namespace TeleFlow.Framework.Updates;

/// <summary>
/// Represents the next step in the update middleware pipeline.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The delegate name intentionally follows established .NET middleware terminology.")]
public delegate Task UpdateDelegate(UpdateContext context);
