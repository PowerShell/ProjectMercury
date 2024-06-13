namespace AIShell.Interpreter.Agent;

/// <summary>
/// Summary description for Class1
/// </summary>
public static class TaskCompletionChatPrompts
{

    public static readonly Dictionary<string, string> prompts = new Dictionary<string, string>
    {
        // Repeating same code
        { "SameError", "\nYou have already told me to fix the error. Please provide more information.\n"},
        // Error handling Function Calling Model
        { "ErrorFunctionsBased", "\nPlease check the ChatRequestToolMessage for the error output from the code. If the code needs user input then say EXACTLY 'Please provide more information'. If it is a Python syntax error try adding a blank line after an indentation is complete.\n" },
        // Error handling Text Based Model
        { "ErrorTextBased", "\nPlease check the following for the error output from the code. If the code needs user input then say EXACTLY 'Please provide more information'. If it is a Python syntax error try adding a blank line after an indentation is complete.\nCode output:\n\n" },
        // Output response for Function Calling Model
        { "OutputFunctionBased", "\nPlease check the ChatRequestToolMessage for output for the code. If this is not what you were expecting then please fix the code. " +
            "If it is what you were expecting please move on to the next step and only the next step. If the task is done say " +
            "EXACTLY 'The task is done.'\n"},
        // Output response for Text Based Model
        { "OutputTextBased", "\nPlease check the following for output for the code. If this is not what you were expecting then please fix the code. " +
                       "If it is what you were expecting please move on to the next step and only the next step. If the task is done say " +
                       "EXACTLY 'The task is done.'\n Code output:\n\n"},
    };
}
