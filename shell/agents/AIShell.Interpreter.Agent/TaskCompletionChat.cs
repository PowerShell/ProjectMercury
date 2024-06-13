using System.Runtime;
using AIShell.Abstraction;

namespace AIShell.Interpreter.Agent;

/// <summary>
/// Manages a task chat session with automated user responses that guide the AI to complete the task.
/// </summary>
internal class TaskCompletionChat
{
    private readonly ChatService _chatService;
    private readonly IHost _host;
    private readonly CodeExecutionService _executionService;
    private readonly Dictionary<string,string> _prompts;
    private readonly BaseModel _model;
    private readonly bool _isFunctionCallingModel;
    private readonly bool _autoExecution;
    private readonly bool _displayErrors;

    /// <summary>
    /// Constructor requires settings for the chat session. Type of model is resolved here.
    /// </summary>
    internal TaskCompletionChat(
        Settings settings,
        ChatService chatService,
        CodeExecutionService executionService,
        IHost Host)
    {
        _host = Host;
        _chatService = chatService;
        _executionService = executionService;
        _prompts = TaskCompletionChatPrompts.prompts;

        _isFunctionCallingModel = settings.ModelInfo.SupportFunctionCalling;
        _autoExecution = settings.AutoExecution;
        _displayErrors = settings.DisplayErrors;

        _model = _isFunctionCallingModel
            ? new FunctionCallingModel(_autoExecution, _displayErrors, _chatService, _executionService, _host)
            : new TextBasedModel(_autoExecution, _displayErrors, _chatService, _executionService, _host);
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
                InternalChatResultsPacket packet = await _model.SmartChat(input, renderingStyle, token);

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
        // Ends automated chat when user cancels during API call or during chat completion streaming.
        if (packet.wasResponseCancelled)
        {
            chatCompleted = true;
        }
        // If there was code given in the response or in a function call enter this block. Otherwise, end the chat.
        else if (packet.wasCodeGiven)
        {
            // When AI corrects code, sometimes it tries the samething again. This automated prompt helps it get unstuck.
            // If the code is different, we can send a different response.
            if(packet.Code.Equals(previousCode) && !string.IsNullOrEmpty(previousCode))
            {
                input = _prompts["SameError"];
            }
            else
            {
                // If user did not run the code, the chat is done. This is to give the user a chance to provide more guidance
                // between steps
                if (packet.didUserRun)
                {
                    // If there was an error in the code for function calling model ask the AI to check ToolResponseMessage.
                    // For text based models, the error is appended to the user message.
                    if (packet.wasThereAnError)
                    {
                        if (_isFunctionCallingModel)
                        {
                            input = _prompts["ErrorFunctionsBased"];
                        }
                        else
                        {
                            input = _prompts["ErrorTextBased"] + packet.toolResponse;
                        }
                    }
                    // Output is handled similiarly to errors.
                    else
                    {
                        if (_isFunctionCallingModel)
                        {
                            input = _prompts["OutputFunctionBased"];
                        }
                        else
                        {
                            input = _prompts["OutputTextBased"] + packet.toolResponse;
                        }
                        previousCode = packet.Code;
                    }
                }
                else
                {
                    chatCompleted = true;
                }
            }
        }
        else
        {
            // If there is no code to run and no tool requests, then the task is done.
            // Different end scenarios can be added here, such as saving the code to a file, saving the chat history, etc.
            chatCompleted = true;
        }
    }
}
