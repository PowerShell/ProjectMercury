# Frequently Asked Questions

This page provides help with common questions about the AISH platform.

## What is AISH?

**AISH** is an AI shell platform that provides a framework for developers to build their own AI
Agents and assistance providers. **AISH** agents provide the user experience for the LLM and are
deeply connected to PowerShell 7. For more about the architecture of **AISH**, see the
[shell/README][01].

## What are agents?

An agent is a library that implements the user interface that talks to a specific large language
model or other assistance provider. Users can interact with these agents in a conversational manner,
using natural language, to get the desired output or assistance. Currently, there are four supported
agents in this repository.

Agent README files:

- [`az-cli` & `az-ps`][02]
- [`openai-gpt`][04]
- [`interpreter`][03]

An assistance provider is an agent that provides user assistance without using a large language
model or AI engine.

## What operating systems are supported?

We have tested **AISH** on macOS and Windows operating systems. **AISH** may work on linux but we
haven't tested it can't guarantee that all features will work as expected.

## How do I get a split pane experience in my Terminal?

The ability to run `aish` in a split pane depends on the capabilities of your terminal. For example,
Windows Terminal can be split by running the following command: `wt -w 0 sp`. Refer to the
documentation for your terminal application to see if it supports this feature.

> [!NOTE]
> Information the user should notice even if skimmingNot all terminal applications support this
> feature.

<!-- link references -->
[01]: ../shell/README.md
[02]: ./shell/ShellCopilot.Azure.Agent/README.md
[03]: ./shell/ShellCopilot.Interpreter.Agent/README.md
[04]: ./shell/ShellCopilot.OpenAI.Agent/README.md
