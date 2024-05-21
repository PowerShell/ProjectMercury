# Frequently Asked Questions

This page provides help with common questions about the AISH platform.

## What is AISH?

AISH is an AI Shell platform that creates a framework for developers to build their own AI Agents. AISH provides the user experience and deeper connection to PowerShell 7+ to agents. To see more about the abstraction layer agents can implement and utilize see [shell/README](../shell/README.md).

## What are agents?

Agents can be thought of as modules that implement the AISH Abstraction layer to talk to a specific large language model or provide some form of assistance when taking in natual language input into a shell prompt. Right now the different agents can be found in the `../shell` folder.

## What operating systems are supported?

We have tested AISH on Mac and Windows operating systems and may work on linux but cannot guarantee all expected behaviors will work. 

## How do I get a split pane experience in my Terminal?

You can get this behavior by splitting your terminal of choice into two panes and then running `aish.exe` in the one of your choice. For example,Windows Terminal can be split by running `wt -w 0 sp`. Please refer to your terminal of choice's documentation on how to split the windows.

