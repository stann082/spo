namespace core;

/// <summary>
/// A failure the user can act on: missing login, bad input, a file that is not there. The message
/// is shown as-is with no stack trace, so it should say what to do next.
/// </summary>
public class SpoException : Exception
{

    public SpoException(string message, int exitCode = 1) : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }

}
