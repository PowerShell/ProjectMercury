# Plugin One Pager

In order to support different kinds of generative A.I models, we need to support a plugin system
that provides the necessary details for users to be able to register and use their own models.

## Motivations

As a shell and generative A.I user, I may not want to just use one model, but many and I want to be
able to register these additional models as easy as possible. For sake of this doc, model is a loose
term being used for some of the basic properties of generative LLM model APIs, i.e endpoint, key,
deployment and prompt.

## Goals

- Create a plugin system that allows for community to contribute their plugins for specific models
- Have all models work with the same basic model management commands for consistency
    - `register`
    - `get`
    - `unregister`
    - etc
- Have a way for user to install various plugins for models
- Support plugins that are not open source that may contain proprietary code and information
- Provide extensibility for plugins to add subcommands to the executable for customization of their specific models.

## Scenarios

1. A user wants to use a plugin that is not supported by the default configurations of the tool

1A. Installing existing plugin

- A user should be able to install a plugin to support the model they want to use. 

1B. Creating a new plugin

- A user should be able to define the schema of the new model's endpoint/architecture that is
  consumed by ShellCopilot and make compatible with ShellCopilots commands
- A user should be able to modify the open source code to add support for different models

## Open questions

- Is management of plugins AND models too much for the users?
- How are plugins distributed? Or are these just built into the code? (how to handled non-open source case then?)
- Should we design in a "generic" way that means we just make ShellCopilot have API calling capabilities and the models just need to have API calls.



