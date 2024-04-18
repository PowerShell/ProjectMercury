using System;

/// <summary>
/// Summary description for Class1
/// </summary>
public static class TaskCompletionChatPrompts
{

    public static readonly Dictionary<string, string> prompts = new Dictionary<string, string>
    {
        // The initial prompt sent with user input
        { "Initial", "\nList out the plan without any code.\n" },
        // General Error handling
        { "Error", "\nFix the error before proceeding to the next step. If the code needs user input then say EXACTLY 'Please provide more information'. If it is a Python syntax error try adding a blank line after an indentation is complete.\n" },
        // Repeating same code
        { "SameError", "\nYou have already told me to fix the error. Please provide more information.\n"},
        // Go to the next step
        { "Next", "\nPlease continue to the next step and only the next step. Do not reiterate all the steps again. Do not respond with more than 1 step.\n" },
        // Force task completion
        { "Force", "\nProceed. You CAN run code on my machine. " +
            "Wait for me to send you the output of the code before telling me the entire task I asked for is done, " +
            "If the task is done say exactly 'The task is done.' If you need some specific " +
            "information (like username or password) say EXACTLY 'Please provide more information.' " +
            "If it's impossible, say 'The task is impossible.' (If I haven't provided a task, say exactly " +
            "'Let me know what you'd like to do next.') Otherwise keep going.\n" },
        // Code output response for function calling model
        { "OutputFunctionBased", "\nPlease check the ChatRequestToolMessage for output for the code. If this is not what you were expecting then please fix the code. " +
            "If it is what you were expecting please move on to the next step and only the next step. If the task is done say " +
            "EXACTLY 'The task is done.'\n"},
        // Code output response for text based model
        { "OutputTextBased", "\nPlease check the following for output for the code. If this is not what you were expecting then please fix the code. " +
                       "If it is what you were expecting please move on to the next step and only the next step. If the task is done say " +
                       "EXACTLY 'The task is done.'\n Code output:\n\n"},
        // Tool Cancelled terminate task
        { "ToolCancelled", "\nTask cancelled. Say exactly 'Let me know what you'd like to do next.'\n" },
        // Use the tool
        { "UseTool", "\nUse the tool I gave you to execute the code.'\n" },
        // User chose not to run code
        { "StopTask", "\nUser chose not to run code. Say exactly 'Would you like to add more details to the task or go a different direction?" +
            " Let me know what you'd like to do next.'\n" },
    };

}
