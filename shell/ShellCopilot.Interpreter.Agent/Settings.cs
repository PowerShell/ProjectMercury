using ShellCopilot.Abstraction;
using System.Text.Json.Serialization;
using System.Security;

namespace ShellCopilot.Interpreter.Agent;

internal class Settings
{
    private readonly GPT _gpt;
    private bool _dirty;

    internal bool Dirty => _dirty;

    public GPT GPT => _gpt;

    public Settings(ConfigData configData)
    {
        _dirty = false;
        _gpt = new GPT(configData.Endpoint,
                       configData.Deployment,
                       configData.ModelName,
                       configData.AutoExecution,
                       configData.DisplayErrors,
                       configData.Key);
    }

    internal async Task<bool> SelfCheck(IHost host, CancellationToken token)
    {
        bool checkPassed = await _gpt.SelfCheck(host, token);
        _dirty |= _gpt.Dirty;

        return checkPassed;
    }

    internal void MarkClean()
    {
        _dirty = false;
    }

    internal ConfigData ToConfigData()
    {
        return new ConfigData()
        {
            Endpoint = _gpt.Endpoint,
            Deployment = _gpt.Deployment,
            ModelName = _gpt.ModelName,
            AutoExecution = _gpt.AutoExecution,
            Key = _gpt.Key,
        };
    }
}

internal class ConfigData
{
    public string Endpoint { set; get; }
    public string Deployment { set; get; }
    public string ModelName { set; get; }
    public bool AutoExecution { set; get; }
    public bool DisplayErrors { set; get; }
             
    [JsonConverter(typeof(SecureStringJsonConverter))]
    public SecureString Key { set; get; }
}
