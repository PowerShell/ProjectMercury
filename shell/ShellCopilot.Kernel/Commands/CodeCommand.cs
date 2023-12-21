using System.Text;
using System.CommandLine;
using Spectre.Console;

namespace ShellCopilot.Kernel.Commands
{
    internal class CodeCommand : CommandBase
    {
        private readonly Shell _shell;

        public CodeCommand(Shell shell)
            : base("code", "Copy or save the code snippet from the last response.")
        {
            _shell = shell;

            var copy = new Command("copy", "Copy the code snippet from the last response to clipboard.");
            var save = new Command("save", "Save the code snippet from the last response to a file.");

            var append = new Option<bool>("--append", "Append to the end of the file.");
            var file = new Argument<FileInfo>("file", "The file path to save the code to.");
            save.AddArgument(file);
            save.AddOption(append);

            AddCommand(copy);
            AddCommand(save);

            copy.SetHandler(CopyAction);
            save.SetHandler(SaveAction, file, append);
        }

        private string GetCodeText()
        {
            List<string> code = _shell.Host.MarkdownRender.GetAllCodeBlocks();
            if (code is not null && code.Count > 0)
            {
                // Use LF as line ending to be consistent with the response from LLM.
                StringBuilder sb = new(capacity: 50);
                for (int i = 0; i < code.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append('\n');
                    }

                    sb.Append(code[i])
                      .Append('\n');
                }

                return sb.ToString();
            }

            return null;
        }

        private void CopyAction()
        {
            string code = GetCodeText();
            if (code is null)
            {
                AnsiConsole.MarkupLine("[olive]No code snippet available for copy.[/]");
                return;
            }

            Clipboard.SetText(code);
            AnsiConsole.MarkupLine("[cyan]Code snippet copied to clipboard.[/]");
        }

        private void SaveAction(FileInfo file, bool append)
        {
            string code = GetCodeText();
            if (code is null)
            {
                AnsiConsole.MarkupLine("[olive]No code snippet available for save.[/]");
                return;
            }

            try
            {
                using FileStream stream = file.Open(append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
                using StreamWriter writer = new(stream, Encoding.Default);

                writer.Write(code);
                writer.Flush();

                AnsiConsole.MarkupLine("[cyan]Code snippet saved to the file.[/]");
            }
            catch (Exception e)
            {
                AnsiConsole.MarkupLine(ConsoleRender.FormatError(e.Message));
            }
        }
    }
}
