using System.CommandLine;

namespace ShellCopilot.Kernel.Commands
{
    internal class ExitCommand : CommandBase
    {
        private readonly Shell _shell;

        public ExitCommand(Shell shell)
            : base("exit", "Exit the interactive session.")
        {
            _shell = shell;
            this.SetHandler(ExitAction);
        }

        private void ExitAction()
        {
            _shell.Exit = true;
        }
    }
}
