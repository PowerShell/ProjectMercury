using System.Text;
using System.Text.Json;
using System.CommandLine;
using AIShell.Abstraction;

namespace AIShell.Kernel.Commands;

internal sealed class RenderCommand : CommandBase
{
    public RenderCommand()
        : base("render", "Render a markdown file, for diagnosis purpose.")
    {
        var file = new Argument<string>("file", "The file path to save the code to.");
        var append = new Option<bool>("--streaming", "Render in the streaming manner.");

        AddArgument(file);
        AddOption(append);
        this.SetHandler(RenderAction, file, append);
    }

    private void RenderAction(string path, bool streaming)
    {
        var host = Shell.Host;

        if (string.IsNullOrEmpty(path))
        {
            host.WriteErrorLine($"Please specify a file path for rendering its content.");
            return;
        }

        path = Utils.ResolveTilde(path);
        FileInfo file = new(path);

        string fullName = file.FullName;
        if (Directory.Exists(fullName))
        {
            host.WriteErrorLine($"The specified path '{fullName}' points to an existing directory. Please specify a file path instead.");
            return;
        }

        try
        {
            using FileStream stream = file.OpenRead();

            if (streaming)
            {
                using var streamingRender = host.NewStreamRender(CancellationToken.None);
                string ext = Path.GetExtension(file.Name);

                if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle JSON file specially as we assume it contains all chunks stored in a string array.
                    string[] words = JsonSerializer.Deserialize<string[]>(stream);
                    foreach (string word in words)
                    {
                        streamingRender.Refresh(word);
                    }
                }
                else
                {
                    using StreamReader reader = new(stream, Encoding.Default);
                    string text = reader.ReadToEnd();
                    string[] words = text.Split(' ');
                    foreach (string word in words)
                    {
                        streamingRender.Refresh(word + " ");
                    }
                }
            }
            else
            {
                using StreamReader reader = new(stream, Encoding.Default);
                string text = reader.ReadToEnd();
                host.RenderFullResponse(text);
            }
        }
        catch (Exception e)
        {
            host.WriteErrorLine(e.Message);
        }
    }
}
