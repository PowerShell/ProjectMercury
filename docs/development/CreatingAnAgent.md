# Creating an Agent

An agent is a module-esc that implements the user interface that talks to a specific large language
model or other assistance provider. Users can interact with these agents in a conversational manner,
using natural language, to get the desired output or assistance. From a more technical point of
view, agents are C# projects that utilize the `ShellCopilot.Abstraction` layer and implement the
`ILLMAgent` interface. 

For details on what the `ShellCopilot.Abstraction` layer and `ShellCopilot.Kernel` provides, see the
[Shell Copilot architecture](../shell/README.md).


## Prerequisites

- .NET 8 SDK
- PowerShell 7.4 or newer

## Steps to create an agent

### Step 1: Create a new project

Currently the only way to import or utilize an agent is for it to be included in the folder
structure of this repository. We suggest creating an agent under the `shell/` folder.


### Step 2: Add the necessary packages

### Step 3: Implement the agent class

### Step 4: Add necessary class members and methods

### Step 5: Modify build script and test out the agent!

## Sharing your agent

Currently there is no way to share your agents in a centralized repository or location. We suggest
forking this repo for development of your own agent or share your agent in the [Discussions](TODO)
tab of this repo under `Agent Share`.

