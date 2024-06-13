namespace AIShell.Interpreter.Agent;

internal enum OutputType
{
    Output,
    Error,
    End,
    Interrupt,
}

internal class OutputData
{
    internal OutputType Type { get; set; }
    internal string Content { get; set; }

    internal OutputData(OutputType type, string content)
    {
        Type = type;
        Content = content;
    }
}
