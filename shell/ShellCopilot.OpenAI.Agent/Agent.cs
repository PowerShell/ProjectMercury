using ShellCopilot.Abstraction;

namespace ShellCopilot.OpenAI.Agent;

public class OpenAIGenericAgent : ILLMAgent
{
    public string Name => "OpenAI.Generic";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    internal bool IsInteractive { private set; get; }
    internal RenderingStyle RenderingStyle { private set; get; }

    public void Initialize(AgentConfig config)
    {
        IsInteractive = config.IsInteractive;
        RenderingStyle = config.RenderingStyle;
        SettingFile = Path.Combine(config.ConfigurationRoot, "openai.agent.json");

        if (!File.Exists(SettingFile))
        {
            NewExampleSettingFile();
        }
    }

    public async Task<bool> SelfCheck(IShellContext shell)
    {
        return await Task.FromResult(false);
    }

    public async Task Chat(string input, IShellContext shell)
    {
        
    }

    private void NewExampleSettingFile()
    {
        File.Create(SettingFile);
    }
}
