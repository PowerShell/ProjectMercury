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
        AddArgument(file);
        this.SetHandler(SaveAction, file);
    }

    private void SaveAction(FileInfo file)
    {
        var host = Shell.Host;

        try
        {
            using FileStream stream = file.OpenRead();
            using StreamReader reader = new(stream, Encoding.Default);

            string text = reader.ReadToEnd();
            host.RenderFullResponse(text);
        }
        catch (Exception e)
        {
            host.WriteErrorLine(e.Message);
        }
    }
}
