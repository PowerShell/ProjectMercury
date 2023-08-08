# Running Command in Shell Copilot

## Why we want it?

It's important to allow the user to refer to their local resources when using Shell Copilot interactively,
so that they can, for example, include content of a file as the context information for a question to the AI.

Take the Bing chat that is built-in to the Edge browser as an example.
It makes it very easy to include the current page as context information when chatting with AI.
As (*mostly*) a counterpart of it in command line,
Shell Copilot should make it easy to include local resources in the chat.

One way to achieve this is to support command invocation in Shell Copilot.

## Overview

We are not going to make Shell Copilot a generic shell like PowerShell or bash,
so the command invocation support will be very scoped.
We can start with just built-in commands, and allow plugin in future to support 3rd party commands.

Scenaros we may want to support:

1. Use _something_ as context for the chat:
   - `use file <file-path>`: read the file content and use it as context.
   - `use url <url>`: get the url (file or web page) and use it as context.
   - `use shell --name <shell-name> --command <command>`: run a command in a shell and use the returned value for context.

2. Save _something_ to file:
   - `save response <file-path>`: save the last response to file.
   - `save codeblock <file-path>`: save the code blocks from the last response to file.

## Implementation

My prototype builds on top of the `System.CommandLine` NuGet package, which provides all the needed supports:

- Create a command and define its arguments, options, and flags
- Create a parser from the command that parses a command-line string
- Invoke a command
- Parsing error and exceution error handling
- Auto generate help content for a command
- Tab completion for the command's arguments and options

All Shell Copilot needs to do is to build a command cache and do command discovery.
Then all the parsing and invocatoin can be handled by the library.

With the parsing and tab completion support, we can build syntax highlighting,
enable tab completion, and support prediction in our read-line utility.

## Sample Code

```c#
class DateCommand : Command
{
    private Argument<string> subjectArgument =
        new ("subject", "The subject of the appointment.");
    private Option<DateTime> dateOption =
        new ("--date", "The day of week to schedule. Should be within one week.");

    public DateCommand() : base("schedule", "Makes an appointment for sometime in the next week.")
    {
        AddArgument(subjectArgument);
        AddOption(dateOption);

        this.SetHandler((subject, date) =>
        {
            Console.WriteLine($"Scheduled \"{subject}\" for {date}");
        },
        subjectArgument, dateOption);
    }
}

static async Task<int> Main()
{
    var dateCommand = new DateCommand();
    var commandLineBuilder = new CommandLineBuilder(dateCommand);
    commandLineBuilder
        .UseHelp()
        .UseSuggestDirective()
        .UseTypoCorrections()
        .UseParseErrorReporting()
        .UseExceptionHandler();
    var parser = commandLineBuilder.Build();

    // Print help content for input like `schedule -h`
    Console.WriteLine("===== schedule -h ===============================================================");
    await parser.InvokeAsync("schedule -h");

    // Run the command based off the command-line string

    Console.WriteLine("===== schedule \"dental appointment\" =============================================");
    await parser.InvokeAsync("schedule \"dental appointment\"");

    // Get tab completion items for the input "schedule <tab>"
    Console.WriteLine("\n");
    Console.WriteLine("===== schedule <tab> =============================================================");
    var completionContext = parser.Parse("schedule ").GetCompletionContext();
    var items = parser.Configuration.RootCommand.GetCompletions(completionContext);
    foreach (CompletionItem item in items)
    {
        Console.WriteLine($"Kind: {item.Kind}, Details: {item.Detail}, SortText: {item.SortText}, InsertText: {item.InsertText}, Label: {item.Label}");
    }

    return 0;
}
```
Output:

```none
PS:14> dotnet run
===== schedule -h ===============================================================
Description:
  Makes an appointment for sometime in the next week.

Usage:
  schedule <subject> [options]

Arguments:
  <subject>  The subject of the appointment.

Options:
  --date <date>   The day of week to schedule. Should be within one week.
  -?, -h, --help  Show help and usage information


===== schedule "dental appointment" =============================================
Scheduled "dental appointment" for 1/1/0001 12:00:00 AM


===== schedule <tab> =============================================================
Kind: Keyword, Details: The day of week to schedule. Should be within one week., SortText: --date, InsertText: --date, Label: --date
Kind: Keyword, Details: Show help and usage information, SortText: --help, InsertText: --help, Label: --help
Kind: Keyword, Details: Show help and usage information, SortText: -?, InsertText: -?, Label: -?
Kind: Keyword, Details: Show help and usage information, SortText: -h, InsertText: -h, Label: -h
Kind: Keyword, Details: Show help and usage information, SortText: /?, InsertText: /?, Label: /?
Kind: Keyword, Details: Show help and usage information, SortText: /h, InsertText: /h, Label: /h
```

## User Experience

In the interactive session of Shell Copilot, a user types `:` to turn on the command mode,
indicating a command is going to be inputed.

PSReadLine will provide tab completion and prediction based on

- command discovery
- tab completion support from `System.CommandLine`
- tab completion for file system entries

PSReadLine can also do syntax highlighting for user input properly by using the parser of the command.

```none
aish:2> :use file e:\yard\docs\one-pager.demo.md

Read "e:\yard\docs\one-pager.demo.md" ... Done
Content of the file will be included as the context for the subsequent chat.

aish:3> please use the `one-pager.demo.md` as a template and genearte a spec for the feature called "auto completon for nature langauge".
```
