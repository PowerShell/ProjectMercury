using ShellCopilot.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellCopilot.Interpreter.Agent;

public interface IModel
{
    public Task<InternalChatResultsPacket> SmartChat(string input, RenderingStyle _renderingStyle);
}

