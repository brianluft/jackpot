using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace J.Server;

public class CustomConsoleFormatter : ConsoleFormatter
{
    public CustomConsoleFormatter()
        : base("CustomConsole") { }

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? _, TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message == null)
            return;

        textWriter.WriteLine(message);
    }
}
