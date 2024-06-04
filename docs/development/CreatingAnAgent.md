# Creating an Agent

An agent is a module-esc that implements the user interface that talks to a specific large language
model or other assistance provider. Users can interact with these agents in a conversational manner,
using natural language, to get the desired output or assistance. From a more technical point of
view, agents are C# projects that utilize the `ShellCopilot.Abstraction` layer and implement the
`ILLMAgent` interface. 

For details on what the `ShellCopilot.Abstraction` layer and `ShellCopilot.Kernel` provides, see the
[Shell Copilot architecture](../shell/README.md).

## Prerequisites

- .NET 8 SDK
- PowerShell 7.4 or newer

## Steps to create an agent

For this example we will be creating an agent to communicate with
[Ollama](https://github.com/ollama/ollama), a CLI tool for managing and using locally built
LLM/SLMs. The agent's folder structure will be `shell/ShellCopilot.Ollama.Agent`.

### Step 1: Create a new project

Currently the only way to import or utilize an agent is for it to be included in the folder
structure of this repository. We suggest creating an agent under the `shell/` folder. Create a new
folder with the prefix `ShellCopilot.<AgentName>` and within that folder create a new C# project
with the same name.

```shell
dotnet new classlib
```

### Step 2: Add the necessary packages

Within the newly created project, add the `ShellCopilot.Abstraction` package as a reference. This is
what your `.csproj` file should look like:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <SuppressNETCoreSdkPreviewMessage>true</SuppressNETCoreSdkPreviewMessage>
  </PropertyGroup>

 
  <ItemGroup>
    <PackageReference Include="ShellCopilot.Abstraction" Version="0.1.0-alpha.11"> 
      <ExcludeAssets>contentFiles</ExcludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

Be sure to replace the version number with the latest version of the package. That can be found in
`shell.common.props` file in the `shell/` folder.

> Note: To reduce the clutter of files you can see the csproj file in the `shell/` folder of the
> agent to reduce PDB and deps.json files from generating during release.

### Step 3: Modify the build script

Early on it is good to modify the build script so you can build and test out your agent as you
develop it. The `build.ps1` script is located in the root of the repository and is used to build the
kernel and all agents. We will be adding the following code to the script to build our agent.

```powershell
$ollama_agent_dir = Join-Path $shell_dir "ShellCopilot.Ollama.Agent"

$ollama_out_dir =  Join-Path $app_out_dir "agents" "ShellCopilot.Ollama.Agent"

if ($LASTEXITCODE -eq 0 -and $AgentToInclude -contains 'ollama') {
    Write-Host "`n[Build the Ollama agent ...]`n" -ForegroundColor Green
    $ollama_csproj = GetProjectFile $ollama_agent_dir
    dotnet publish $ollama_csproj -c $Configuration -o $ollama_out_dir
}
```

Be sure to put this code after definition of the `$shell_dir`, `$app_out_dir`, and
`$AgentToInclude`. Also add the name of the agent to the `$AgentToInclude` array.
```powershell
$AgentToInclude ??= @('openai-gpt', 'interpreter', 'ollama')
```

### Step 3: Implement the agent class

Now lets start building the agent, modify the `Class1.cs` file to implement the `ILLMAgent` interface. We suggest renaming the file to `OllamaAgent.cs` and the class to `OllamaAgent`.

```csharp
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

public sealed class OllamaAgent : ILLMAgent
{
    
}
```

### Step 4: Add necessary class members and methods

```csharp
public sealed class OllamaAgent : ILLMAgent
{
    // Name of the agent
    public string Name => "ollama";

    // Description displayed on start up
    public string Description => "This is an AI assistant that utilizes Ollama"; // TODO prerequistates for running this agent

    // This is the company added to /like and /dislike verbage for who the telemetry helps.
    public string Company => "Microsoft";

    // These are samples that are shown at start up
    public List<string> SampleQueries => [
        "How do I list files in a given directory?"
    ];

    // These are any legal/additional information links you want to provide at start up
    public Dictionary<string, string> LegalLinks { private set; get; }
    
    private OllamaChatService _chatService;

    // Text to be rendered at the end.
    private StringBuilder _text; 

    public void Dispose()
    {
        _chatService?.Dispose();
    }

    public void Initialize(AgentConfig config)
    {
        _text = new StringBuilder();
        _chatService = new OllamaChatService();

        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ollama Docs"] = "https://github.com/ollama/ollama",
        };

    }

    // 
    public IEnumerable<CommandBase> GetCommands() => null;

    public string SettingFile { private set; get; } = null;

    public void RefreshChat() {}

    public bool CanAcceptFeedback(UserAction action) => false;
    
    public void OnUserAction(UserActionPayload actionPayload) {}

    // Main chat functions
    public async Task<bool> Chat(string input, IShell shell)
    {
        
    }
}
```

Getting "Hello World!" from the agent you can add this code to your `Chat` method.

```csharp
public async Task<bool> Chat(string input, IShell shell)
{
    // Get the shell host
    IHost host = shell.Host; 

    // get the cancelation token
    CancellationToken token = shell.CancellationToken; 

    try
    {
       host.RenderFullResponse("Hello World!");
    }
    catch (OperationCanceledException e)
    {
        _text.AppendLine(e.ToString());

        host.RenderFullResponse(_text.ToString());
        
        return false;
    }
    
    return true;
}

```
### Step 5: Modify build script and test out the agent!

## Sharing your agent

Currently there is no way to share your agents in a centralized repository or location. We suggest
forking this repo for development of your own agent or share your agent in the [Discussions](TODO)
tab of this repo under `Agent Share`.

