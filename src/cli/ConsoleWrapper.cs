namespace cli;

public static class ConsoleWrapper
{

    private static readonly object lockObject = new object();

    #region Public Methods

    public static void WriteLine(string message, ConsoleColor color)
    {
        Write(Console.Out, message, color);
    }

    public static void WriteSuccess(string message)
    {
        Write(Console.Out, message, ConsoleColor.Green);
    }

    /// <summary>Notices go to stderr so they never end up in piped output.</summary>
    public static void WriteInfo(string message)
    {
        Write(Console.Error, message, ConsoleColor.Yellow);
    }

    public static void WriteError(string message)
    {
        Write(Console.Error, message, ConsoleColor.Red);
    }

    #endregion

    #region Helper Methods

    private static void Write(TextWriter writer, string message, ConsoleColor color)
    {
        lock (lockObject)
        {
            Console.ForegroundColor = color;
            writer.WriteLine(message);
            Console.ResetColor();
        }
    }

    #endregion

}
