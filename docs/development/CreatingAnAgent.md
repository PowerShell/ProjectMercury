# Creating an Agent

An agent is a code library that interfaces with the ShellCopilot to talk to a specific large
language model or other assistance provider. Users chat with the agents using natural language to
get the desired output or assistance. Agents are implemented as C# classes that implement the
`ILLMAgent` interface from the `ShellCopilot.Abstraction` package.

For details about the `ShellCopilot.Abstraction` layer and `ShellCopilot.Kernel`, see the
[Shell Copilot architecture][03] documentation.

## Prerequisites

- .NET 8 SDK or newer
- PowerShell 7.4 or newer

## Steps to create an agent

For this example we create an agent to communicate with [Ollama][04], a CLI tool for managing and
using locally built LLM/SLMs. The agent is stored in the `shell/ShellCopilot.Ollama.Agent` folder of
the repository.

### Step 1: Create a new project

Currently, the only way to import an agent is for it to be included in the folder structure of this
repository. We suggest creating an agent under the `shell/` folder. Create a new folder with the
prefix `ShellCopilot.<AgentName>`. Within that folder, create a new C# project with the same name.
Run the following command from the folder where you want to create the agent:

```shell
dotnet new classlib
```

### Step 2: Add the necessary packages

Within the newly created project, add a reference to the `ShellCopilot.Abstraction` package. To
reduce the number of files created by the build, you can disable the generation of PDB and deps.json
for release builds.

Your `.csproj` file should contain the following elements:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <SuppressNETCoreSdkPreviewMessage>true</SuppressNETCoreSdkPreviewMessage>

    <!-- Disable deps.json generation -->
    <GenerateDependencyFile>false</GenerateDependencyFile>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <!-- Disable PDB generation for the Release build -->
    <DebugSymbols>false</DebugSymbols>
    <DebugType>None</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ShellCopilot.Abstraction" Version="0.1.0-alpha.11">
      <ExcludeAssets>contentFiles</ExcludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

</Project>
```

> [!IMPORTANT]
> Be sure to replace the version number with the latest version of the package. That can be found in
> the [`shell/shell.common.props`][01] file.

### Step 3: Modify the build script

Modify the build script so that you can build and test your agent during development. The
`build.ps1` script is located in the root of the repository. This script builds the kernel and all
agents. The following lines were added to the script to build the new agent.

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

To being the creation of the agent, modify the `Class1.cs` file to implement the `ILLMAgent`
interface. We suggest renaming the file to `OllamaAgent.cs` and the rename class to `OllamaAgent`.
We've also added some packages that are used by the code in the implementation.

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

Next, implement the necessary variables and methods of the agent class. The comments provide
descriptions of the members of the **OllamaAgent** class. The `_chatService` member is an instance
of the **OllamaChatService** class. The implementation of the **OllamaChatService** class is show in
later steps.

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
            ["Prerequisites"] = "https://aka.ms/ollama/readme"
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

For the initial implementation, we want the agent to return "Hello World!" to prove that we have
create the correct interfaces. We will also add a `try-catch` block to catch any expections to
handle when the user tries to cancel the operation.

Add the following code to your `Chat` method.

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

### Step 5: Build your agent

At this point its good to try building and testing the agent. See if you get `Hello World!` when you
ask a question.

Use the following command to build the agent:

```powershell
../../build.ps1
```

To test the agent, run the `aish` you just built. The build script puts the path to `aish` on the
clipboard. Paste the path from the clipboard into your terminal application. Select your agent from
the list of agents presented by `aish`.

```
Shell Copilot
v0.1.0-preview.1

Please select an agent to use:

    az-ps
    az-cli
    interpreter
   >ollama
    openai-gpt
```

After selecting the agent, enter a question in the chat window. You should see the response "Hello
World!".

### Step 6: Add utility functions

Next, we want to add a utility class to help us check that ollama is installed and running on the
computer. The utility class contains two methods:

- one to test that the ollama CLI is installed
- one to test that the ollama API port is open on local host

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

Next, add a tests to the chat method using these utility methods.

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

### Step 7: Create data structures to exchange data with the Chat Service

Before we can use the ollama API, we need to create classes that handle input to and responses from
the ollama API. To find more information about the API call we're going to make see, this The
following [ollama example][05] shows the format of the input and the response from the agent.

For this example we call the ollama API with streaming disabled. This generates a single, fixed
response. In the future we could add streaming capabilities so that responses could be rendered in
real time, as the agent receives them.

To defined the data structures, create a new file called `OllamaSchema.cs` in the same folder.

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

Now we have the pieces we need to construc a chat service that communicates using the ollama API. A
separate chat service class isn't required but can be helpful to abstract the calls to the API.

Create a new file called `OllamaChatService.cs` in the same folder as the agent. For this example,
we are using a hard coded endpoint and model for the ollama API. In the future, we could add these
as parameters in an agent configuration file.

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

### Step 8: Call the chat service

Now we can call the chat service in the main agent class.

Modify the `Chat` method to call the chat service and render the response to the user. The following
code shows the completed `Chat` method.

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

Congratulations! The agent is now complete. You can build and test the agent to confirm it's
working. Compare your code to the example code in the [`shell/ShellCopilot.Ollama.Agent`][02] folder
to see if you missed a step.

## How can I share my own agent?

Currently there is no way to share your agents in a centralized repository. We suggest forking this
repository for development of your own agent. You can share a link your fork in the `Agent Sharing`
section of the [Discussions][06] tab of this repository.

<!-- updated link references -->
[01]: ../../shell/shell.common.props
[02]: ../../shell/ShellCopilot.Ollama.Agent/
[03]: ../../shell/README.md
[04]: https://github.com/ollama/ollama
[05]: https://github.com/ollama/ollama/blob/main/docs/api.md#request-no-streaming
[06]: https://github.com/PowerShell/AISH/discussions/categories/agent-sharing
