using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using Azure;

namespace Microsoft.PowerShell.Copilot
{
    internal class OpenAI
    {
        private const string API_ENV_VAR = "AZURE_OPENAI_API_KEY";
        internal const string ENDPOINT_ENV_VAR = "AZURE_OPENAI_ENDPOINT";
        internal const string SYSTEM_PROMPT_ENV_VAR = "AZURE_OPENAI_SYSTEM_PROMPT";

        private const string API_SUB_VAR = "API_SUB_KEY";

        private static SecureString _subKey;
        private static readonly string[] SPINNER = new string[8] {"🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘"};
        private static List<string> _promptHistory = new();
        private static List<string> _assistHistory = new();
        private static int _maxHistory = 256;
        internal static string _lastCodeSnippet = string.Empty;
        OpenAIClient client;

        string endpoint;
        private static string _os = GetOS();

        public OpenAI()
        {
            if (_subKey is null)
            {
                _subKey = GetSubscriptionKey();
                //endpoint - curently API management service gateway url
            }
            endpoint = "https://myapian.azure-api.net/";
            string key = "placeholder";
            OpenAIClientOptions options = new OpenAIClientOptions();
            string subscriptionKey = Environment.GetEnvironmentVariable(API_SUB_VAR);

            //adds policy
            AzureKeyCredentialPolicy policy = new AzureKeyCredentialPolicy(new AzureKeyCredential(subscriptionKey), "Ocp-Apim-Subscription-Key");
            options.AddPolicy(policy, Azure.Core.HttpPipelinePosition.PerRetry);

            //creates client
            client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key), options);
        }

        internal string LastCodeSnippet()
        {
            return _lastCodeSnippet;
        }

        internal void SendPrompt(string input, bool debug, CancellationToken cancelToken)
        {
            try
            {
                var task = new Task<string>(() =>
                {
                    return GetCompletion(input, debug, cancelToken);
                });
                task.Start();
                int i = 0;
                int cursorTop = Console.CursorTop;
                bool taskCompleted = false;
                while (!taskCompleted && task.Status != TaskStatus.RanToCompletion)
                {
                    switch (task.Status)
                    {
                        case TaskStatus.Canceled:
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task was cancelled.");
                            taskCompleted = true;
                            break;
                        case TaskStatus.Faulted:
                            Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}Task faulted.");
                            taskCompleted = true;
                            break;
                        default:
                            break;
                    }

                    Console.CursorTop = cursorTop;
                    Console.CursorLeft = 0;
                    Console.CursorVisible = false;
                    Console.Write($"{Screenbuffer.RESET}{task.Status}... {SPINNER[i++ % SPINNER.Length]}".PadRight(Console.WindowWidth));
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                        if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers == ConsoleModifiers.Control)
                        {
                            EnterCopilot.Cancel();
                            Console.CursorVisible = true;
                            break;
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }
                Console.CursorTop = cursorTop;
                Console.CursorLeft = 0;
                Console.Write(" ".PadRight(Console.WindowWidth));
                Console.CursorVisible = true;
                var output = task.Result;
                _assistHistory.Add(output);
                _promptHistory.Add(input);
                if (_assistHistory.Count > _maxHistory)
                {
                    _assistHistory.RemoveAt(0);
                    _promptHistory.RemoveAt(0);
                }

                GetCodeSnippet(output);

                // colorize code sections
                // split into lines
                var lines = output.Split(new[] { '\n' });
                var colorOutput = new StringBuilder();
                var codeSnippet = new StringBuilder();
                bool inCode = false;
                foreach (var line in lines)
                {
                    if (line.StartsWith("```powershell"))
                    {
                        inCode = true;
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightBlack}```");
                    }
                    else if (line.StartsWith("```"))
                    {
                        inCode = false;
                        colorOutput.Append(Formatting.GetPrettyPowerShellScript(codeSnippet.ToString()));
                        codeSnippet.Clear();
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightBlack}```");
                    }
                    else if (inCode)
                    {
                        codeSnippet.AppendLine(line);
                    }
                    else
                    {
                        colorOutput.AppendLine($"{PSStyle.Instance.Foreground.BrightYellow}{line}");
                    }
                }

                Screenbuffer.WriteConsole($"{colorOutput.ToString()}{Screenbuffer.RESET}");
            }
            catch (Exception e)
            {
                Screenbuffer.WriteLineConsole($"{PSStyle.Instance.Foreground.BrightRed}EXCEPTION: {e.Message}\n{e.StackTrace}");
            }
        }
        internal string GetCompletion(string prompt, bool debug, CancellationToken cancelToken)
        {
            try
            {
                ChatCompletionsOptions requestBody = GetRequestBody(prompt);
                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: RequestBody:\n{Formatting.GetPrettyJson(requestBody.ToString())}");
                }

                if (debug)
                {
                    Console.WriteLine($"{PSStyle.Instance.Foreground.BrightMagenta}DEBUG: OpenAI URL: {endpoint}");
                }


                Response<ChatCompletions> response = client.GetChatCompletions(
                deploymentOrModelName: "gpt4",
                requestBody);
                ChatCompletions chatCompletions = response.Value;
                var output = "\n";

                foreach (ChatChoice choice in chatCompletions.Choices)
                {
                    output += choice.Message.Content;
                }

                return output;
            }
            catch (OperationCanceledException)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}Operation cancelled.";
            }
            catch (Exception e)
            {
                return $"{PSStyle.Instance.Foreground.BrightRed}HTTP EXCEPTION: {e.Message}\n{e.StackTrace}";
            }
        }

        private static string GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "macos";
            }
            else
            {
                return "unknown";
            }
        }

        private static ChatCompletionsOptions GetRequestBody(string prompt)
        {
            ChatCompletionsOptions requestBody = new ChatCompletionsOptions();
            var messages = requestBody.Messages;
            string system_prompt = Environment.GetEnvironmentVariable(SYSTEM_PROMPT_ENV_VAR);
            if (system_prompt is null)
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "system",
                        content: $"You are an AI assistant with experise in PowerShell, Azure, and the command line.  Assume user is using {_os} operating system unless specified. You are helpful, creative, clever, and very friendly. Responses including PowerShell code are enclosed in ```powershell blocks."
                    )
                );
            }
            else
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "system",
                        content: system_prompt
                    )
                );
            }

            for (int i = 0; i < _assistHistory.Count; i++)
            {
                messages.Add(
                    new ChatMessage
                    (
                        role: "user",
                        content: _promptHistory[i]
                    )
                );
                messages.Add(
                    new ChatMessage
                    (
                        role: "assistant",
                        content: _assistHistory[i]
                    )
                );
            }

            messages.Add(
                new ChatMessage
                (
                    role: "user",
                    content: prompt
                )
            );

            requestBody.MaxTokens = 4096;
            requestBody.Temperature = (float?) 0.7;
            return requestBody;
        }

        private static void GetCodeSnippet(string input)
        {
            // split input into lines
            var lines = input.Split(new[] { '\n' });
            var codeSnippet = new StringBuilder();
            // find the first line that starts with ```powershell and copy the lines until ``` is found
            // TODO: handle case where there isn't a PowerShell but just a command-line block
            bool foundStart = false;
            bool foundEnd = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("```powershell"))
                {
                    foundStart = true;
                }
                else if (line.StartsWith("```"))
                {
                    foundEnd = true;
                }
                else if (foundStart && !foundEnd)
                {
                    codeSnippet.AppendLine(line);
                }
            }

            _lastCodeSnippet = codeSnippet.ToString();
        }

        private static string ConvertSecureString(SecureString ss)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(ss);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        private SecureString GetSubscriptionKey()
        {
            string key = Environment.GetEnvironmentVariable(API_SUB_VAR);
            if (key is null)
            {
                throw(new Exception($"{API_SUB_VAR} environment variable not set"));
            }
            else
            {
                var ss = new SecureString();
                foreach (char c in key)
                {
                    ss.AppendChar(c);
                }

                return ss;
            }
        }

        internal class AzureKeyCredentialPolicy : HttpPipelineSynchronousPolicy
        {
            
            private readonly string _name;
            private readonly AzureKeyCredential _credential;
            private readonly string  _prefix;

            /// <summary>
            /// Initializes a new instance of the <see cref="AzureKeyCredentialPolicy"/> class.
            /// </summary>
            /// <param name="credential">The <see cref="AzureKeyCredential"/> used to authenticate requests.</param>
            /// <param name="name">The name of the key header used for the credential.</param>
            /// <param name="prefix">The prefix to apply before the credential key. For example, a prefix of "SharedAccessKey" would result in
            /// a value of "SharedAccessKey {credential.Key}" being stamped on the request header with header key of <paramref name="name"/>.</param>
            public AzureKeyCredentialPolicy(AzureKeyCredential credential, string name, object prefix = null)
            {
                _credential = credential;
                _name = name;
                if(_prefix != null)
                {
                    _prefix = (string) prefix;
                }
            }

            /// <inheritdoc/>
            public override void OnSendingRequest(Azure.Core.HttpMessage message)
            {
                base.OnSendingRequest(message);
                message.Request.Headers.SetValue(_name, _prefix != null ? $"{_prefix} {_credential.Key}" :  _credential.Key);
            }
        }
    }
}
