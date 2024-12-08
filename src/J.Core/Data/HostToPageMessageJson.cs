namespace J.Core.Data;

/// <summary>
/// Represents a message sent from the web browser host (C#) to the web page (JavaScript).
/// </summary>
/// <param name="Type">Message type.</param>
public readonly record struct HostToPageMessageJson(string Type);
