namespace J.Core.Data;

/// <summary>
/// Represents a message sent from the web page (JavaScript) to the web browser host (C#).
/// </summary>
/// <param name="Type">Message type.</param>
/// <param name="Ids">The list of movie or tag IDs (optional).</param>
public readonly record struct PageToHostMessageJson(string Type, List<string>? Ids);
