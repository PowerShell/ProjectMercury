using System.Text;
using System.CommandLine;
using Spectre.Console;

namespace ShellCopilot.Kernel.Commands
{
    internal class ClearCommand : CommandBase
    {
        public ClearCommand()
            : base("cls", "Clear the screen.")
        {
            this.SetHandler(ClearAction);
        }

        private void ClearAction()
        {
            Console.Clear();
        }
    }
}
