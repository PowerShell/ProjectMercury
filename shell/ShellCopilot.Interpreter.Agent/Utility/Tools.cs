using Azure.AI.OpenAI;
using System;
using System.Text.Json;

namespace ShellCopilot.Interpreter.Agent
{
    public class Tools
	{
        public Tools()
        {

        }

        public static ChatCompletionsFunctionToolDefinition RunCode = new()
        {
            Name = "execute",
            Description = "This function is able to run given python code. This will allow you to execute python code " +
            "on my local machine.",
            Parameters = BinaryData.FromObjectAsJson(
            new
            {
                Type = "object",
                Properties = new
                {
                    Language = new
                    {
                        Type = "string",
                        Description = "The programming language (required parameter to the `execute` function)",
                        Enum = new[] { "python","powershell" },
                    },
                    Code = new
                    {
                        Type = "string",
                        Description = "The code to be executed (required parameter to the `execute` function)",
                    }
                },
                Required = new[] { "language","code" }
            },
            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        };
    }
}
