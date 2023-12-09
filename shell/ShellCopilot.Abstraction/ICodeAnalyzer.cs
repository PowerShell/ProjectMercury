namespace ShellCopilot.Abstraction;

public interface ICodeAnalyzer : IChatService
{
    bool AnalyzeCode(List<string> codeBlocks, IShellContext shell, out string explanation);
}
