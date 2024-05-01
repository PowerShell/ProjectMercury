using System.Runtime;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Interpreter.Agent;

/// <summary>
/// Manages a task chat session with automated user responses that guide the AI to complete the task.
/// </summary>
internal class TaskCompletionChat
{
    private ChatService _chatService;
    private IHost host;
    private CodeExecutionService _executionService;
    private Dictionary<string,string> prompts = TaskCompletionChatPrompts.prompts;
    private BaseModel model;
    private bool _isFunctionCallingModel;
    private bool _autoExecution;
    private bool _displayErrors;

    /// <summary>
    /// Constructor requires settings for the chat session. Type of model is resolved here.
    /// </summary>
    internal TaskCompletionChat(
        Settings settings,
        ChatService chatService,
        CodeExecutionService executionService,
        IHost Host)
    {
        _isFunctionCallingModel = ModelInfo.IsFunctionCallingModel(settings.ModelName);
		_autoExecution = settings.AutoExecution;
        _displayErrors = settings.DisplayErrors;
        _chatService = chatService;
        _executionService = executionService;
		host = Host;

        if(_isFunctionCallingModel)
        {
            model = new FunctionCallingModel(_autoExecution, _displayErrors, _chatService, _executionService, host);
        }
        else
        {
            model = new TextBasedModel(_autoExecution, _displayErrors, _chatService, _executionService, host);
        }
	}
    
    /// <summary>
    /// This method contains the while loop that manages the automated chat session.
    /// All AI responses and code exeuction results are reduced to boolean values that determine the next automated user response.
    /// </summary>
    public async Task<bool> StartTask(string input, RenderingStyle renderingStyle, CancellationToken token)
    {
        bool chatCompleted = false;
        string previousCode = "";

        while (!chatCompleted)
        {
            if (string.IsNullOrEmpty(input))
            {
                break;
            }
            try
            {
                InternalChatResultsPacket packet = await model.SmartChat(input, renderingStyle, token);

                AutomatedUserResponses(ref input, ref chatCompleted, ref previousCode, packet);
            }
            catch (OperationCanceledException)
            {
                // Ignore the exception
            }
        }

        return chatCompleted;
    }

    /// <summary>
    /// Resolves packet booleans into a user response or completes the chat session.
    /// </summary>
    private void AutomatedUserResponses(
        ref string input, 
        ref bool chatCompleted, 
        ref string previousCode, 
        InternalChatResultsPacket packet)
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
                            if (_isFunctionCallingModel)
                            {
                                input = prompts["Error"];
                            }
                            else
                            {
                                input = prompts["Error"] + packet.toolResponse;
                            }
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
