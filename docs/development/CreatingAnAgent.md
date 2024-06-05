# Creating an Agent

An agent is a module-esc package that utilizes the user interface that the ShellCopilot provides to
talks to a specific large language model or other assistance provider. Users can use these agents to
create a conversational chat using natural language, to get the desired output or assistance. Agents
are C# classes that utilize the `ShellCopilot.Abstraction` layer and implement the `ILLMAgent`
interface.

For details on what the `ShellCopilot.Abstraction` layer and `ShellCopilot.Kernel` provides, see the
[Shell Copilot architecture](../shell/README.md).

## Prerequisites

- .NET 8 SDK or newer
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
[`shell.common.props`](../../shell/shell.common.props) file in the `shell/` folder.

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
`$AgentToInclude`. Also add the name of the agent to the `$AgentToInclude` array and parameter
validation.
```powershell
$AgentToInclude ??= @('openai-gpt', 'interpreter', 'ollama')
```

### Step 3: Implement the agent class

Now lets start building the agent, modify the `Class1.cs` file to implement the `ILLMAgent`
interface. We suggest renaming the file to `OllamaAgent.cs` and the class to `OllamaAgent`. We have
also added a number of packages that will be useful as we develop the agent.

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

Lets now implement the necessary variables and methods for the agent. Below are descriptions of each
and the values we will use for the Ollama agent. There is a
`private OllamaChatService _chatService;` implementation which we will do later in the steps.


```csharp
public sealed class OllamaAgent : ILLMAgent
{
    /// <summary>
    /// The name of the agent
    /// </summary>
    public string Name => "ollama";

    /// <summary>
    /// The description of the agent to be shown at start up
    /// </summary>
    public string Description => "This is an AI assistant that utilizes the Ollama CLI tool. Be sure to follow all prerequisites in aka.ms/ollama/readme"; 

    /// <summary>
    /// This is the company added to /like and /dislike verbiage for who the telemetry helps.
    /// </summary>
    public string Company => "Microsoft";

    /// <summary>
    /// These are samples that are shown at start up for good questions to ask the agent
    /// </summary>
    public List<string> SampleQueries => [
        "How do I list files in a given directory?"
    ];

    /// <summary>
    /// These are any optional legal/additional information links you want to provide at start up
    /// </summary>
    public Dictionary<string, string> LegalLinks { private set; get; }

    /// <summary>
    /// This is the chat service to call the API from
    /// </summary>
    private OllamaChatService _chatService;

    /// <summary>
    /// A string builder to render the text at the end
    /// </summary>
    private StringBuilder _text; 

    /// <summary>
    /// Dispose method to clean up the unmanaged resource of the chatService
    /// </summary>
    public void Dispose()
    {
        _chatService?.Dispose();
    }

    /// <summary>
    /// Initializing function for the class when the shell registers an agent
    /// </summary>
    /// <param name="config">Agent configuration for any configuration file and other settings</param>
    public void Initialize(AgentConfig config)
    {
        _text = new StringBuilder();
        _chatService = new OllamaChatService();

        LegalLinks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Ollama Docs"] = "https://github.com/ollama/ollama",
            ["Prerequisites"] = "aka.ms/ollama/readme"
        };

    }

    /// <summary>
    /// Get commands that an agent can register to the shell when being loaded
    /// </summary>
    public IEnumerable<CommandBase> GetCommands() => null;

    /// <summary>
    /// Gets the path to the setting file of the agent.
    /// </summary>
    public string SettingFile { private set; get; } = null;

    /// <summary>
    /// Refresh the current chat by starting a new chat session.
    /// An agent can reset chat states in this method.
    /// </summary>
    public void RefreshChat() {}

    /// <summary>
    /// Gets a value indicating whether the agent accepts a specific user action feedback.
    /// </summary>
    /// <param name="action">The user action.</param>
    public bool CanAcceptFeedback(UserAction action) => false;

    /// <summary>
    /// A user action was taken against the last response from this agent.
    /// </summary>
    /// <param name="action">Type of the action.</param>
    /// <param name="actionPayload"></param>
    public void OnUserAction(UserActionPayload actionPayload) {}

    /// <summary>
    /// Main chat function that takes 
    /// </summary>
    /// <param name="input">The user input from the chat experience</param>
    /// <param name="shell">The shell that provides host functionality</param>
    /// <returns>Task Boolean that indicates whether the query was served by the agent.</returns>
    public async Task<bool> Chat(string input, IShell shell)
    {
        
    }  
}
```

Getting "Hello World!" from the agent you can add this code to your `Chat` method. We will also add
a try catch to catch any expections where the user is trying to cancel the operation.

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
At this point its good to try building the agent seeing if you get `Hello World!` when you ask a
question.

### Step 5: Adding Utils and checks

Before we start calling the ollama API, lets add a utility class to help us check that ollama is
installed and running on the users computer. We will add two functions, one to check if the ollama
CLI is installed and the other to check if the ollama API is running by checking if the port is open
on local host.

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace ShellCopilot.Ollama.Agent;

internal static class Utils
{
    /// <summary>
    /// Confirms a given CLI tool is installed on the system
    /// </summary>
    /// <param name="toolName">CLI tools name to check</param>
    /// <returns>Boolean whether or not the CLI tool is installed</returns>
    public static bool IsCliToolInstalled(string toolName)
    {
        string shellCommand, shellArgument;
        // Determine the shell command and arguments based on the OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            shellCommand = "cmd.exe";
            shellArgument = "/c " + toolName + " --version";
        }
        else
        {
            shellCommand = "/bin/bash";
            shellArgument = "-c \"" + toolName + " --version\"";
        }

        try
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo(shellCommand, shellArgument)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = procStartInfo;
                process.Start();

                // You can read the output or error if necessary for further processing
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Confirms a localhost port is open to ensure ollama server is running
    /// </summary>
    /// <param name="port">port number to check against</param>
    /// <returns>Boolean whether or not the localhost port is responding</returns>
    public static bool IsPortResponding(int port)
    {
        using (TcpClient tcpClient = new TcpClient())
        {
            try
            {
                // Attempt to connect to the specified port on localhost
                tcpClient.Connect("localhost", port);
                return true;
            }
            catch (SocketException ex)
            {
                return false; 
            }
        }
    }
}
```

Now that we have these utility functions we can add a check to the chat method to ensure that ollama
is installed and running before we call the API. 

```csharp
public async Task<bool> Chat(string input, IShell shell)
{
    // Get the shell host
    IHost host = shell.Host; 

    // get the cancellation token
    CancellationToken token = shell.CancellationToken; 

    try
    {
        // Check that ollama is installed
        if (!Utils.IsCliToolInstalled("ollama")){
            host.RenderFullResponse("Please be sure ollama is installed and running a server, check all the prerequisites in the README of this agent.");
            return false;
        } 

        // Check that server is running
        if (!Utils.IsPortResponding(11434)){
            host.RenderFullResponse("It seems you may not have the ollama server running please be sure to have `ollama serve` running and check the prerequisites in the README of this agent.");
            return false;
        }
        
        // Where we will put the call to the API
        
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
### Step 6: Creating a Chat Service and Schema for response


Before we call the ollama API lets create a few classes to help us be able to handle the responses.
We will create a schema for the data to be sent to the ollama API, a schema for the response data
from the ollama API and a response schema to help with the API calls. To find more information about
the API call we are going to make see, this
[ollama example](https://github.com/ollama/ollama/blob/main/docs/api.md#request-no-streaming). For
this example we will be calling the ollama API to generate a response without streaming and a fixed
model. In the future we can add streaming capabilities so that the responses are rendered in real
time as the agent receives them. Lets create a new file called `OllamaSchema.cs` in the same folder.

```csharp
namespace ShellCopilot.Ollama.Agent;

// Query class for the data to send to the endpoint
internal class Query
{
    public string prompt { get; set; }
    public string model { get; set; }

    public bool stream { get; set; }
}

// Response data schema
internal class ResponseData
{
    public string model { get; set; }
    public string created_at { get; set; }
    public string response { get; set; }
    public bool done { get; set; }
    public string done_reason { get; set; }
    public int[] context { get; set; }
    public double total_duration { get; set; }
    public long load_duration { get; set; }
    public int prompt_eval_count { get; set; }
    public int prompt_eval_duration { get; set; }
    public int eval_count { get; set; }
    public long eval_duration { get; set; }
}

internal class OllamaResponse
{
    public int Status { get; set; }
    public string Error { get; set; }
    public string Api_version { get; set; }
    public ResponseData Data { get; set; }
}
```

Once we have this schema we can more easily put together a chat service to help us communicate with
the API. A separate chat service class is not required but can be helpful to abstract the calls to
the API. Lets create a new file called `OllamaChatService.cs` in the same folder as the agent. Here
we will hard code the endpoint and model for the ollama API. In the future we can add these as
parameters to an agent configuration file.

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using ShellCopilot.Abstraction;

namespace ShellCopilot.Ollama.Agent;

internal class OllamaChatService : IDisposable
{
    /// <summary>
    /// Ollama endpoint to call to generate a response
    /// </summary>
    internal const string Endpoint = "http://localhost:11434/api/generate";

    /// <summary>
    /// Http client 
    /// </summary>
    private readonly HttpClient _client;

    /// <summary>
    /// Initialization method to initialize the http client 
    /// </summary>

    internal OllamaChatService()
    {
        _client = new HttpClient();
    }

    /// <summary>
    /// Dispose of the http client 
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    /// Preparing chat with data to be sent
    /// </summary>
    /// <param name="input">The user input from the chat experience</param>
    /// <returns>The HTTP request message</returns>
    private HttpRequestMessage PrepareForChat(string input)
    {
        // Main data to send to the endpoint
        var requestData = new Query
        {
            model = "phi3",
            prompt = input,
            stream = false
        };

        var json = JsonSerializer.Serialize(requestData);

        var data = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = data };

        return request;
    }

    /// <summary>
    /// Getting the chat response async
    /// </summary>
    /// <param name="context">Interface for the status context used when displaying a spinner.</param>
    /// <param name="input">The user input from the chat experience</param>
    /// <param name="cancellationToken">The cancellation token to exit out of request</param>
    /// <returns>Response data from the API call</returns>
    internal async Task<ResponseData> GetChatResponseAsync(IStatusContext context, string input, CancellationToken cancellationToken)
    {
        try
        {
            HttpRequestMessage request = PrepareForChat(input);
            HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            context?.Status("Receiving Payload ...");
            Console.Write(response.Content);
            var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            return JsonSerializer.Deserialize<ResponseData>(content);
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled by user.
        }

        return null;
    }
}

```

### Step 7: Calling the chat service

Now its time to actually call the chat service in the main agent class. We will modify the `Chat`
method to call the chat service and render the response to the user. Here is what a finalized `Chat`
function should look like.

```csharp
public async Task<bool> Chat(string input, IShell shell)
{
    // Get the shell host
    IHost host = shell.Host; 

    // get the cancellation token
    CancellationToken token = shell.CancellationToken; 

    try
    {
        // Check that ollama is installed
        if (!Utils.IsCliToolInstalled("ollama")){
            host.RenderFullResponse("Please be sure ollama is installed and running a server, check all the prerequisites in the README of this agent.");
            return false;
        } 

        // Check that server is running
        if (!Utils.IsPortResponding(11434)){
            host.RenderFullResponse("It seems you may not have the ollama server running please be sure to have `ollama serve` running and check the prerequisites in the README of this agent.");
            return false;
        }
        
        ResponseData ollamaResponse = await host.RunWithSpinnerAsync(
            status: "Thinking ...",
            func: async context => await _chatService.GetChatResponseAsync(context, input, token)
        ).ConfigureAwait(false);

        if (ollamaResponse is not null)
        {
            // render the content
            host.RenderFullResponse(ollamaResponse.response); 
        }
        
        
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

Congrats! The agent is now complete and you can now build and test the agent to confirm it is
working. To find the fully completed code to check if you missed a step you can find it in the
[`shell/ShellCopilot.Ollama.Agent`](../../shell/ShellCopilot.Ollama.Agent/) folder.

## Sharing your own agent

Currently there is no way to share your agents in a centralized repository or location. We suggest
forking this repo for development of your own agent or share your agent in the [Discussions](TODO)
tab of this repo under `Agent Share`.

