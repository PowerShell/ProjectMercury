using System.Text;
using System.CommandLine;
using ShellCopilot.Abstraction;

namespace ShellCopilot.Kernel.Commands;

internal sealed class RenderCommand : CommandBase
{
    public RenderCommand()
        : base("render", "Render a markdown file, for diagnosis purpose.")
    {
        var file = new Argument<FileInfo>("file", "The file path to save the code to.");
        var append = new Option<bool>("--streaming", "Render in the streaming manner.");

        AddArgument(file);
        AddOption(append);
        this.SetHandler(SaveAction, file, append);
    }

    private void SaveAction(FileInfo file, bool streaming)
    {
        var host = Shell.Host;

        try
        {
            using FileStream stream = file.OpenRead();
            using StreamReader reader = new(stream, Encoding.Default);
            string text = reader.ReadToEnd();

            if (streaming)
            {
                using var streamingRender = host.NewStreamRender(CancellationToken.None);
                string[] words = text.Split(' ');
                foreach (string word in words)
                {
                    streamingRender.Refresh(word + " ");
                }
            }
            else
            {
                host.RenderFullResponse(text);
            }
        }
        catch (Exception e)
        {
            host.WriteErrorLine(e.Message);
        }
    }
}
