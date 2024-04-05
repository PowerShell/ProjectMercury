using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;
/// <summary>
/// Summary description for Class1
/// </summary>
internal class TaskCompletionChat
{
	private ChatService _chatService;
	private IHost host;
    private Computer computer;
    private Dictionary<string,string> prompts = TaskCompletionChatPrompts.prompts;
    private IModel model;
    private bool _isFunctionCallingModel;
    private bool _autoExecution;
    private bool _displayErrors;

	internal TaskCompletionChat(
        bool isFunctionCallingModel, 
        bool autoExecution, 
        bool displayErrors,
        ChatService chatService, 
        IHost Host)
	{
        _isFunctionCallingModel = isFunctionCallingModel;
		_autoExecution = autoExecution;
        _displayErrors = displayErrors;
        _chatService = chatService;
		host = Host;

        computer = new();

        if(_isFunctionCallingModel)
        {
            model = new FunctionCallingModel(_autoExecution, _displayErrors, _chatService, host);
        }
        else
        {
            model = new TextBasedModel(_autoExecution, _displayErrors, _chatService, host);
        }
	}

    internal void CleanUpProcesses()
    {
        computer.Terminate();
    }

    public async Task<bool> StartTask(string input, RenderingStyle _renderingStyle, CancellationToken token)
    {
        bool chatCompleted = false;
        bool askToSave = false;
        string previousCode = "";
        //input += prompts["Initial"];
        while (!chatCompleted)
        {
            if (string.IsNullOrEmpty(input))
            {
                break;
            }
            try
            {
                InternalChatResultsPacket packet = await model.SmartChat(input, _renderingStyle, token);

                PromptEngineering(ref input, ref chatCompleted, ref askToSave, ref previousCode, packet, token);

                if (askToSave)
                {
                    // Save the task
                    // bool saveChoice = await host.PromptForConfirmationAsync("Would you like to save the conversation?", true, token);
                    // if (saveChoice)
                    // {
                    //     string fileName = await host.PromptForSecretAsync("Please enter the file name: ", token);
                    //     if(!string.IsNullOrEmpty(fileName))
                    //     {
                    //         _chatService.SaveHistory(fileName);
                    //     }
                    // }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the exception
            }
        }

        return chatCompleted;
    }

    private void PromptEngineering(
        ref string input, 
        ref bool chatCompleted, 
        ref bool askToSave,
        ref string previousCode, 
        InternalChatResultsPacket packet, 
        CancellationToken token)
    {
        if (packet.wasResponseCancelled)
        {
            chatCompleted = true;
        }
        else if (packet.wasCodeGiven && !packet.didNotCallTool)
        {
            if(packet.Code.Equals(previousCode) && !string.IsNullOrEmpty(previousCode))
            {
                input = prompts["SameError"];
            }
            else if(packet.didNotCallTool)
            {
                input = prompts["UseTool"];
            }
            else if (packet.wasToolSupported && packet.languageSupported)
            {
                if (packet.didUserRun)
                {
                    if (packet.wasToolCancelled)
                    {
                        input = prompts["ToolCancelled"];
                    }
                    else
                    {
                        if (packet.wasThereAnError)
                        {
                            input = prompts["Error"];
                        }
                        else
                        {
                            if (_isFunctionCallingModel)
                            {
                                input = prompts["OutputFunctionBased"];
                            }
                            else
                            {
                                input = prompts["OutputTextBased"] + packet.toolResponse;
                            }
                            previousCode = packet.Code;
                        }
                    }
                }
                else
                {
                    // input = prompts["StopTask"];
                    chatCompleted = true;
                }
            }
            else
            {
                input = packet.toolResponse + prompts["Force"];
            }
        }
        else
        {
            if (packet.isTaskComplete)
            {
                chatCompleted = true;
                askToSave = true;
            }
            else if (packet.isTaskImpossible)
            {
                chatCompleted = true;
            }
            else if (packet.isMoreInformationNeeded)
            {
                chatCompleted = true;
            }
            else if(packet.isNoTaskPresent)
            {
                chatCompleted = true;
            }
            else
            {
                chatCompleted = true;
            }
        }
    }
}
