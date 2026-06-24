namespace TeleFlow.Core.Updates;

/// <summary>
/// Marks a transport-specific update envelope that can enter the TeleFlow core pipeline.
/// </summary>
/// <remarks>
/// This interface intentionally has no members. Core uses it as a typed, transport-agnostic
/// boundary instead of accepting arbitrary <see cref="object" /> payloads.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "TeleFlow.Core uses this marker as a typed transport-agnostic update boundary.")]
public interface IUpdatePayload
{
}
