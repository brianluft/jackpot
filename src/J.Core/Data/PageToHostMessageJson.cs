namespace J.Core.Data;

/// <summary>
/// Represents a message sent from the web page (JavaScript) to the web browser host (C#).
/// </summary>
/// <param name="Type">"search"</param>
public readonly record struct PageToHostMessageJson(string Type);
