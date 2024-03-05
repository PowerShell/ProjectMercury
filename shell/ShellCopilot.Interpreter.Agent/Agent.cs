using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.OpenAI;
using ShellCopilot.Abstraction;
using Azure;
using System.Diagnostics;
using Newtonsoft.Json;

namespace ShellCopilot.Interpreter.Agent;

public sealed class InterpreterAgent : ILLMAgent
{
    public string Name => "interpreter-gpt";
    public string Description { private set; get; }
    public string SettingFile { private set; get; }

    private const string SettingFileName = "openai.agent.json";
    private bool _isInteractive;
    private bool _refreshSettings;
    private bool _isDisposed;
    private string _configRoot;
    private string _historyRoot;
    private RenderingStyle _renderingStyle;
    private Settings _settings;
    private FileSystemWatcher _watcher;
    private ChatService _chatService;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        _watcher.Dispose();
        _isDisposed = true;
    }

    /// <inheritdoc/>
    public void Initialize(AgentConfig config)
    {
        // while (!System.Diagnostics.Debugger.IsAttached)
        // {
        //     System.Threading.Thread.Sleep(200);
        // }
        // System.Diagnostics.Debugger.Break();

        _isInteractive = config.IsInteractive;
        _renderingStyle = config.RenderingStyle;
        _configRoot = config.ConfigurationRoot;

        SettingFile = Path.Combine(_configRoot, SettingFileName);
        if (!File.Exists(SettingFile))
        {
            NewExampleSettingFile();
        }

        _historyRoot = Path.Combine(_configRoot, "history");
        Directory.CreateDirectory(_historyRoot);

        _settings = ReadSettings();
        _chatService = new ChatService(_isInteractive, _historyRoot, _settings);

        Description = "An agent leverages GPTs that target OpenAI backends. Currently not ready to server queries.";
        GPT active = _settings.Active;
        if (active is not null)
        {
            Description = $"Active GTP: {active.Name}. {active.Description}";
        }

        _watcher = new FileSystemWatcher(_configRoot, SettingFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnSettingFileChange;
    }

    /// <inheritdoc/>
    public IEnumerable<CommandBase> GetCommands()
    {
        return null;
    }

    private async Task<List<string>> SmartChat(string input, IShell shell, Orchestrator orch)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;
        List<string> executionResult = new();
        Dictionary<int, string> toolCallIdsByIndex = new();
        Dictionary<int, string> functionNamesByIndex = new();
        Dictionary<int, StringBuilder> functionArgumentBuildersByIndex = new();

        if (_refreshSettings)
        {
            _settings = ReadSettings();
            _chatService.RefreshSettings(_settings);
            _refreshSettings = false;
        }

        string responseContent = null;
        if (_renderingStyle is RenderingStyle.FullResponsePreferred)
        {
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<Response<ChatCompletions>> func_non_streaming() => _chatService.GetChatCompletionsAsync(chatRequestUserMessage, token);
            Response<ChatCompletions> response = await host.RunWithSpinnerAsync(func_non_streaming).ConfigureAwait(false);

            if (response is not null)
            {
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                host.RenderFullResponse(responseContent);

                ChatChoice responseChoice = response.Value.Choices[0];
                if (responseChoice.FinishReason is CompletionsFinishReason FinishReason)
                {
                    string warning = GetWarningBasedOnFinishReason(FinishReason);
                    if (warning is not null)
                    {
                        host.MarkupWarningLine(warning);
                        host.WriteLine();
                    }
                }
                // _chatService.AddToolCallToHistory(response);
            }
        }
        else
        {
            ChatRequestUserMessage chatRequestUserMessage = new(input);
            Task<StreamingResponse<StreamingChatCompletionsUpdate>> func_streaming() => _chatService.GetStreamingChatResponseAsync(chatRequestUserMessage, token);
            StreamingResponse<StreamingChatCompletionsUpdate> response = await host.RunWithSpinnerAsync(func_streaming).ConfigureAwait(false);
            
            if (response is not null)
            {
                using var streamingRender = host.NewStreamRender(token);
                try
                {
                    // Cannot pass in `cancellationToken` to `GetChoicesStreaming()` and `GetMessageStreaming()` methods.
                    // Doing so will result in an exception in Azure.Open
                    await foreach (StreamingChatCompletionsUpdate chatUpdate in response)
                    {
                        if (_chatService.IsFunctionCallingModel())
                        {
                            if (chatUpdate.ToolCallUpdate is StreamingFunctionToolCallUpdate functionToolCallUpdate)
                            {
                                if (functionToolCallUpdate.Id != null)
                                {
                                    toolCallIdsByIndex[functionToolCallUpdate.ToolCallIndex] = functionToolCallUpdate.Id;
                                }
                                if (functionToolCallUpdate.Name != null)
                                {
                                    functionNamesByIndex[functionToolCallUpdate.ToolCallIndex] = functionToolCallUpdate.Name;
                                }
                                if (functionToolCallUpdate.ArgumentsUpdate != null)
                                {
                                    StringBuilder argumentsBuilder
                                        = functionArgumentBuildersByIndex.TryGetValue(
                                            functionToolCallUpdate.ToolCallIndex,
                                            out StringBuilder existingBuilder) ? existingBuilder : new StringBuilder();
                                    argumentsBuilder.Append(functionToolCallUpdate.ArgumentsUpdate);
                                    functionArgumentBuildersByIndex[functionToolCallUpdate.ToolCallIndex] = argumentsBuilder;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
                        {
                            streamingRender.Refresh(chatUpdate.ContentUpdate);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ignore the cancellation exception.
                    throw;
                }
                responseContent = streamingRender.AccumulatedContent;
            }
        }

        if (_chatService.IsFunctionCallingModel())
        {
            ChatRequestAssistantMessage assistantHistoryMessage = new(responseContent);
            foreach (KeyValuePair<int, string> indexIdPair in toolCallIdsByIndex)
            {
                ChatCompletionsFunctionToolCall toolCall = new ChatCompletionsFunctionToolCall(
                                   id: indexIdPair.Value,
                                   functionNamesByIndex[indexIdPair.Key],
                                   functionArgumentBuildersByIndex[indexIdPair.Key].ToString());
                assistantHistoryMessage.ToolCalls.Add(toolCall);
                _chatService.AddResponseToHistory(assistantHistoryMessage);
                string arguments = toolCall.Arguments;
                string language = "";
                string code = "";
                if (arguments != null)
                {
                    Dictionary<string, string> argumentsDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(arguments);
                    language = argumentsDict["language"];
                    code = argumentsDict["code"];
                    host.RenderFullResponse($"```{language}\n{code}\n```");
                }
                bool runChoice = await host.PromptForConfirmationAsync("Would you like to run the code?",true, token);
                if( runChoice )
                {
                    ChatRequestToolMessage toolResponse = await _chatService.GetToolCallResponseMessage(toolCall, orch, shell.CancellationToken);
                    if (toolResponse.Content is not null)
                    {
                        host.RenderFullResponse($"```{language}:\n{toolResponse.Content}\n```");
                        _chatService.AddResponseToHistory(toolResponse);
                        executionResult.Add(toolResponse.Content);
                        executionResult.Add(code);
                        return executionResult;
                    }
                }
                else
                {
                    executionResult.Add("Did not run code");
                    executionResult.Add(responseContent);
                    return executionResult;
                }
            }
            _chatService.AddResponseToHistory(assistantHistoryMessage);
        }
        else
        {
            if (orch.IsCodeBlockComplete(responseContent))
            {
                 _chatService.AddResponseToHistory(new ChatRequestAssistantMessage(responseContent));
                 return await ExecuteProvidedCode(orch, shell, responseContent);
            }
        }

        executionResult.Add("No code was given");
        executionResult.Add(responseContent);

        return executionResult;
    }

    /// <inheritdoc/>
    public async Task<bool> Chat(string input, IShell shell)
    {
        IHost host = shell.Host;
        CancellationToken token = shell.CancellationToken;
        Orchestrator orch = new(token);
        bool checkPass = await SelfCheck(host, token);
        if (!checkPass)
        {
            host.MarkupWarningLine($"[[{Name}]]: Cannot serve the query due to the missing configuration. Please properly update the setting file.");
            return checkPass;
        }
        List<string> UserResponseOptions = new List<string>
        {
            "\nFix the error before proceeding to the next step. If the code needs user input then ask directly in english.",
            "\nPlease continue to the next step and only the next step. Do not reiterate all the steps again. Do not response with more than 1 step. Use tools.",
            "\nProceed. You CAN run code on my machine. " +
            "Wait for me to send you the output of the code before telling me the entire task I asked for is done, " +
            "If the task is done say exactly 'The task is done.' If you need some specific " +
            "information (like username or password) say EXACTLY 'Please provide more information.' " +
            "If it's impossible, say 'The task is impossible.' (If I haven't provided a task, say exactly " +
            "'Let me know what you'd like to do next.') Otherwise keep going. Do not repeat yourself.",
        };
        bool chatCompleted = false;
        string previousCode = "";
        input += "\nList out the plan without any code.";
        
        while(!chatCompleted)
        {
            Stopwatch timer = new();
            timer.Restart();
            timer.Start();
            try
            {
                // TODO: come up with a better system for results sent to and from languages to orchestrator to chat
                // Needs to be more readable and less error prone
                // TODO: Edit line 251 for a more coherent output report to GPT
                // first element is the error or output, second element is the code language
                List<string> chatResult = await SmartChat(input, shell, orch);
                string resultCode = chatResult[0];
                string executionResult = chatResult[1];
                _chatService.AddResponseToHistory(new ChatRequestUserMessage("This is a test."));

                // If there is an error, fix it and continue
                if (resultCode.StartsWith("Error:"))
                {
                    input = resultCode + UserResponseOptions[0] + UserResponseOptions[2];
                    orch = new(token);
                    if (previousCode.Equals(executionResult))
                    {
                        input = "You have already told me to fix the error. Please provide more information." + UserResponseOptions[2];
                    }
                    else
                    {
                        previousCode = executionResult;
                    }
                }
                // If code runs, continue to the next step
                else if(resultCode.StartsWith("Success:"))
                {
                    input = $"The Following was the output from {executionResult}: " + resultCode + "\n" + "Is this the output you were expecting? If not please fix it. If so then do the following: " + UserResponseOptions[1];
                }
                else if(resultCode.Equals("Did not run code"))
                {
                    input = UserResponseOptions[1];
                }
                else if(resultCode.StartsWith("No code was given"))
                {
                    if(executionResult.Contains("done"))
                    {
                        chatCompleted = true;
                        bool saveChoice = await host.PromptForConfirmationAsync("Would you like to save the python file?", true, token);
                        if (!saveChoice)
                        {
                            orch._python.DeleteTempFile();
                        }
                        else
                        {
                            host.WriteLine($"The file has been saved as {orch._python.GetTempFile()}.");
                        }
                        continue;
                    
                    }
                    else if(executionResult.Contains("The task is impossible."))
                    {
                        chatCompleted = true;
                        bool saveChoice = await host.PromptForConfirmationAsync("Would you like to save the python file?", true, token);
                        if (!saveChoice)
                        {
                            orch._python.DeleteTempFile();
                        }
                        else
                        {
                            host.WriteLine($"The file has been saved as {orch._python.GetTempFile()}.");
                        }
                        continue;
                    }
                    else if(executionResult.Contains("Please provide", StringComparison.CurrentCultureIgnoreCase))
                    {
                        chatCompleted = true;
                        continue;
                    }
                    else if(executionResult.Contains("Let me know what you'd like to do next", StringComparison.CurrentCultureIgnoreCase))
                    {
                        chatCompleted = true;
                        continue;
                    }
                    else
                    {
                        input = UserResponseOptions[2];
                    }
                }
                else
                {
                    input = UserResponseOptions[2];
                }
            }
            catch(OperationCanceledException)
            {
                chatCompleted = true;
            }
            timer.Stop();
            if(timer.ElapsedMilliseconds < 8000)
            {
                await Task.Delay(8000 - (int)timer.ElapsedMilliseconds);
            }
        }

        return checkPass;
    }

    private async Task<List<string>> ExecuteProvidedCode(Orchestrator orch, IShell shell, string responseContent)
    {
        IHost host = shell.Host;
        List<string> executionResult = new();
        bool choice = await host.PromptForConfirmationAsync($"\nWould you like to run the {orch.CodeBlock.Key} code?", true, shell.CancellationToken);
        if (!choice)
        {
            executionResult.Add("Did not run code");
        }
        else
        {
            Task<string> func() => orch.RunCode(orch.CodeBlock.Key, orch.CodeBlock.Value);
            string output = await host.RunWithSpinnerAsync(func, "Running the code...");
            host.RenderFullResponse("```\n" + output + "\n");
            if (output.StartsWith("Error:"))
            {
                executionResult.Add("Error: " + output);
            }
            else
            {
                executionResult.Add("Success: " + output);
            }
        }
        executionResult.Add(orch.CodeBlock.Value);
        return executionResult;
    }

    internal async Task<bool> SelfCheck(IHost host, CancellationToken token)
    {
        bool checkPass = await _settings.SelfCheck(host, token);

        if (_settings.Dirty)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                SaveSettings(_settings);
                _settings.MarkClean();
            }
            finally
            {
                _watcher.EnableRaisingEvents = true;
            }
        }

        return checkPass;
    }

    private static string GetWarningBasedOnFinishReason(CompletionsFinishReason reason)
    {
        if (reason.Equals(CompletionsFinishReason.TokenLimitReached))
        {
            return "The response was incomplete as the max token limit was exhausted.";
        }

        if (reason.Equals(CompletionsFinishReason.ContentFiltered))
        {
            return "The response was truncated as it was identified as potentially sensitive per content moderation policies.";
        }

        return null;
    }

    private Settings ReadSettings()
    {
        Settings settings = null;
        if (File.Exists(SettingFile))
        {
            using var stream = new FileStream(SettingFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            };
            var data = System.Text.Json.JsonSerializer.Deserialize<ConfigData>(stream, options);
            settings = new Settings(data);
        }

        return settings;
    }

    private void SaveSettings(Settings config)
    {
        using var stream = new FileStream(SettingFile, FileMode.Create, FileAccess.Write, FileShare.None);
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        System.Text.Json.JsonSerializer.Serialize(stream, config.ToConfigData(), options);
    }

    private void OnSettingFileChange(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType is WatcherChangeTypes.Changed)
        {
            _refreshSettings = true;
        }
    }

    private void NewExampleSettingFile()
    {
        string SampleContent = $@"
{{
  // Declare GPT instances.
  ""GPTs"": [
    // To use Azure OpenAI as the AI completion service:
    // - Set `Endpoint` to the endpoint of your Azure OpenAI service,
    //   or the endpoint to the Azure API Management service if you are using it as a gateway.
    // - Set `Deployment` to the deployment name of your Azure OpenAI service.
    // - Set `Key` to the access key of your Azure OpenAI service,
    //   or the key of the Azure API Management service if you are using it as a gateway.
    {{
      ""Name"": ""powershell-ai"",
      ""Description"": ""A GPT instance with expertise in PowerShell scripting and command line utilities."",
      ""Endpoint"": ""{Utils.ShellCopilotEndpoint}"",
      ""Deployment"": ""gpt4"",
      ""ModelName"": ""gpt-4-0314"",   // required field to infer properties of the service, such as token limit.
      ""Key"": null,
      ""SystemPrompt"": ""You are Open Interpreter, a world-class programmer that can complete any goal by executing code.
                        Write only python and powershell script. Do not write bash or any other language.
                        Respond in the following way: 
                        First, list out the plan without any code.
                        Second, install any necessary packages in the first steps.
                        Thrid, go through the plan one step at a time and, if applicable, write the code for each step.
                        Finally, do not show me how to run the code.
                        When a user refers to a filename, they're likely referring to an existing file in the directory you're currently executing code in.
                        Write messages to the user in Markdown.
                        In general, try to **make plans** with as few steps as possible. As for actually executing code to 
                        carry out that plan. You should 
                        try something, print information about it, then continue from there in tiny, informed steps. You will 
                        never get it on the first try, and attempting it in one go will often lead to errors you cant see.
                        You are capable of **any** task. If there are any libraries to be installed, give me the powershell command to install it.
                        
                        [User Info]
                        Operating System: {Utils.OS}"",
                       

    // To use the public OpenAI as the AI completion service:
    // - Ignore the `Endpoint` and `Deployment` keys.
    // - Set `Key` to be the OpenAI access token.
    // For example:
    /*
    {{
        ""Name"": ""python-ai"",
        ""Description"": ""A GPT instance that acts as an expert in python programming that can generate python code based on user's query."",
        ""ModelName"": ""gpt-4"",
        ""Key"": null,
        ""SystemPrompt"": ""example-system-prompt""
    }}
    */
  ],

  // Specify the GPT instance to use for user query.
  ""Active"": ""powershell-ai""
}}
";
        File.WriteAllText(SettingFile, SampleContent, Encoding.UTF8);
    }
}
