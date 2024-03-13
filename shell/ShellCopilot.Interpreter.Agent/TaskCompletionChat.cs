using Azure.AI.OpenAI;
using Azure;
using Newtonsoft.Json;
using ShellCopilot.Abstraction;
using System;
using System.Runtime;
using System.Text;
using System.Diagnostics;
namespace ShellCopilot.Interpreter.Agent;
/// <summary>
/// Summary description for Class1
/// </summary>
internal class TaskCompletionChat
{
	private ChatService _chatService;
	private IHost host;
    private Computer computer;
    private CancellationToken token;
    private Dictionary<string,string> prompts = TaskCompletionChatPrompts.prompts;
    private IModel model;

	internal TaskCompletionChat(bool _isFunctionCallingModel, ChatService chatService, IHost Host, CancellationToken Token)
	{
		_chatService = chatService;
		host = Host;
        token = Token;
        computer = new();
        if(_isFunctionCallingModel)
        {
            model = new FunctionCallingModel(_chatService, host, token);
        }
        else
        {
            model = new TextBasedModel(_chatService, host, token);
        }
	}

    internal void CleanUpProcesses()
    {
        computer.Terminate();
    }

    public async Task<bool> StartTask(string input, RenderingStyle _renderingStyle)
    {
        bool chatCompleted = false;
        string previousCode = "";
        //input += prompts["Initial"];
        while (!chatCompleted)
        {
            try
            {
                InternalChatResultsPacket packet = await model.SmartChat(input, _renderingStyle);

                PromptEngineering(ref input, ref chatCompleted, ref previousCode, packet);
            }
            catch (OperationCanceledException)
            {
                chatCompleted = true;
                throw;
            }
        }

        return chatCompleted;
    }

    private void PromptEngineering(ref string input, ref bool chatCompleted, ref string previousCode, InternalChatResultsPacket packet)
    {
        if (packet.wasCodeGiven && !packet.didNotCallTool)
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
                            input = prompts["Output"] + packet.toolResponse;
                            previousCode = packet.Code;
                        }
                    }
                }
                else
                {
                    input = prompts["StopTask"];
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
                //TODO: add a way to save the file
                chatCompleted = true;
                computer.Terminate();
            }
            else if (packet.isTaskImpossible)
            {
                //TODO: add a way to save the file
                chatCompleted = true;
                computer.Terminate();
            }
            else if (packet.isMoreInformationNeeded)
            {
                chatCompleted = true;
            }
            else if(packet.isNoTaskPresent)
            {
                chatCompleted = true;
            }
            else if (packet.didNotCallTool)
            {
                input = prompts["UseTool"];
            }
            else
            {
                input = prompts["Force"];
            }
        }
    }
}
