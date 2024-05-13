using System.Text;
using System.CommandLine;
using AISH.Abstraction;

namespace AISH.Kernel.Commands;

internal sealed class CodeCommand : CommandBase
{
    public CodeCommand()
        : base("code", "Command to interact with the code generated.")
    {
        var copy = new Command("copy", "Copy the code snippet from the last response to clipboard.");
        var save = new Command("save", "Save the code snippet from the last response to a file.");
        var post = new Command("post", "Post the code snippet from the last response to the connected command-line shell.");

        var nth = new Argument<int>("n", () => -1, "The n-th (starts from 1) code block to copy or post.");
        nth.AddValidator(result => {
            int value = result.GetValueForArgument(nth);
            if (value is not -1 && value < 1)
            {
                result.ErrorMessage = "The argument <n> must be equal to or greater than 1.";
            }
        });
        copy.AddArgument(nth);
        post.AddArgument(nth);

        var append = new Option<bool>("--append", "Append to the end of the file.");
        var file = new Argument<FileInfo>("file", "The file path to save the code to.");
        save.AddArgument(file);
        save.AddOption(append);

        AddCommand(copy);
        AddCommand(save);
        AddCommand(post);

        copy.SetHandler(CopyAction, nth);
        save.SetHandler(SaveAction, file, append);
        post.SetHandler(PostAction, nth);
    }

    private static string GetCodeText(Shell shell, int index)
    {
        List<CodeBlock> code = shell.GetCodeBlockFromLastResponse();

        if (code is null || code.Count is 0 || index >= code.Count)
        {
            return null;
        }

        // The index being -1 means to combine all code blocks.
        if (index is -1)
        {
            // Use LF as line ending to be consistent with the response from LLM.
            StringBuilder sb = new(capacity: 50);
            for (int i = 0; i < code.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(code[i].Code).Append('\n');
            }

            return sb.ToString();
        }

        // Otherwise, return the specific code block.
        return code[index].Code;
    }

    private void CopyAction(int nth)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        int index = nth > 0 ? nth - 1 : nth;
        string code = GetCodeText(shell, index);

        if (code is null)
        {
            host.MarkupLine("[olive]No code snippet available for copy.[/]");
            return;
        }

        Clipboard.SetText(code);
        host.MarkupLine("[cyan]Code snippet copied to clipboard.[/]");
        shell.OnUserAction(new CodePayload(UserAction.CodeCopy, code));
    }

    private void SaveAction(FileInfo file, bool append)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        string code = GetCodeText(shell, index: -1);
        if (code is null)
        {
            host.MarkupLine("[olive]No code snippet available for save.[/]");
            return;
        }

        try
        {
            using FileStream stream = file.Open(append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream, Encoding.Default);

            writer.Write(code);
            writer.Flush();

            host.MarkupLine("[cyan]Code snippet saved to the file.[/]");
            shell.OnUserAction(new CodePayload(UserAction.CodeSave, code));
        }
        catch (Exception e)
        {
            host.WriteErrorLine(e.Message);
        }
    }

    private void PostAction(int nth)
    {
        var shell = (Shell)Shell;
        var host = shell.Host;

        int index = nth > 0 ? nth - 1 : nth;
        List<string> codeToPost = null;
        List<CodeBlock> allCode = shell.GetCodeBlockFromLastResponse();

        if (allCode is not null && allCode.Count > 0)
        {
            if (index is -1)
            {
                codeToPost = new(capacity: allCode.Count);
                foreach (CodeBlock item in allCode)
                {
                    codeToPost.Add(item.Code);
                }
            }
            else if (index < allCode.Count)
            {
                codeToPost = [allCode[index].Code];
            }
        }

        if (codeToPost is null)
        {
            host.MarkupLine("[olive]No code snippet available to post.[/]");
            return;
        }

        try
        {
            shell.Channel.PostCode(new PostCodeMessage(codeToPost));
            host.MarkupLine("[cyan]Code snippet was successfully posted.[/]");
        }
        catch (Exception e)
        {
            host.WriteErrorLine(e.Message);
        }
    }
}
