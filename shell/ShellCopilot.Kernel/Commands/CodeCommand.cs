using System;
using System.CommandLine;
using System.Text;

using Spectre.Console;

namespace ShellCopilot.Kernel.Commands
{
    internal class CodeCommand : Command
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

        private void CopyAction()
        {
            string code = _shell.MarkdownRender.GetLastCodeBlock();
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
            string code = _shell.MarkdownRender.GetLastCodeBlock();
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
