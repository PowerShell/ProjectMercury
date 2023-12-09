namespace ShellCopilot.Abstraction;

public interface IChatService
{
    string Name { get; }
    string Description { get; }
    string SettingFile { get; }

    Task Chat(string input, IShellContext shell);

    void Initialize(ServiceConfig config);
}

public enum RenderingStyle
{
    FullResponsePreferred,
    StreamingResponsePreferred,
}

public class ServiceConfig
{
    public string ConfigurationRoot { get; }
    public RenderingStyle RenderingStyle { get; }
}
