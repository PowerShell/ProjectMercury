# Frequently Asked Questions

This page provides help with common questions about Project Mercury.

## What is Project Mercury?

**Project Mercury** is a platform that provides a framework for developers to build their own AI
Agents and assistance providers for an AI Shell. Agents provide the user experience for the LLM and are
deeply connected to PowerShell 7. For more about the architecture, see the
[shell/README][01].

## What are agents?

An agent is a library that implements the user interface that talks to a specific language
model or other assistance provider. Users can interact with these agents in a conversational manner,
using natural language, to get the desired output or assistance. Currently, these are the supported
agents:

Agent README files:

- [`az-cli` & `az-ps`][05]
- [`openai-gpt`][04]
- [`ollama`][02]
- [`interpreter`][03]

An assistance provider is an agent that provides user assistance without using a language
model or AI engine.

## What operating systems are supported?

We have tested on macOS and Windows operating systems. **Project Mercury** may work on linux but we
haven't tested it can't guarantee that all features will work as expected.

## How do I get a split pane experience in my Terminal?

The ability to run `aish` in a split pane depends on the capabilities of your terminal. For example,
Windows Terminal can be split by running the following command: `wt -w 0 sp`. Refer to the
documentation for your terminal application to see if it supports this feature.

> [!NOTE]
> Not all terminal applications support this feature.

<!-- link references -->
[01]: ../shell/README.md
[02]: ../shell/agents/AIShell.Ollama.Agent/README.md
[03]: ../shell/agents/AIShell.Interpreter.Agent/README.md
[04]: ../shell/agents/AIShell.OpenAI.Agent/README.md
[05]: ../shell/agents/AIShell.Azure.Agent/README.md
