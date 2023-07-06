# Some More Scenarios

## Scenario 1

A user should be able to use `ai` non-interactively -- for an ad-hoc question instead of a chat conversation -- by running `ai` with a question message:

```pwsh
ai "how to convert byte array to base64 encoded string"
```

A user should be able to pipe text content to `ai` as the context information for the question message:

```pwsh
$url = 'https://gist.githubusercontent.com/daxian-dbw/38f2cf2eb4e3a37f316dad73ca551e94/raw/c9929d101a112c142bf22b9e59fd2ecfef178e59/Add-User.ps1'
irm $url | ai 'explain what this code snippet does'
```

To support this scenario, `ai` should

- Take an optional string argument as the `question message`.
- Take input from a redirected stdin.

```pwsh
ai [options] [question-message]
```

> [NOTE] See [mods](https://github.com/charmbracelet/mods) for the UX around this usecase.


## Scenario 2

When being used in the alternate screen buffer along side with a shell application,
the user would want to switch back and forth between the main and alternate screen buffers
through key-bindings (shell integration), to use the shell and `ai` alternately.

We want to keep the chat conversation history in this scenario,
so when switching from `ai` to shell,
we should save the chat history so far.

When switching back to `ai` from shell,
we should read the saved chat history and write them all out,
so from the user's view, they are back to where they were.

However, when the user explicitly runs `ai` to start a new interactive session,
it should start fresh, without any previous chat history.
Therefore, when swtiching to `ai` with the shell key-binding,
it must start `ai` differently so that `ai` knows it needs to re-populate the chat history.

To support this difference, `ai` should

- Have the option `--restore`.
  - When this option is specified, `ai` tries to load previous chat history at startup.
  - When this option is NOT specified, `ai` starts fresh.

The shell key-binding handler will call `ai --restore`, so as to mimic uninterrupted chat conversation.

## Scenario 3

Multiple shells could be using `ai` in the alternate screen buffer at the same time.
So, when saving chat history, it's important to tell which history file belongs to which session,
so that we don't mix up the chat history.

To support this, `ai` should get its parent process id -- the shell process that starts `ai`,
and then use the shell process id in the name of the chat history file.

In this way, when switching back to `ai`, the shell process should be the same,
and thus we can again get the parent process id (shell process),
and use it to locate the right chat-history file.
