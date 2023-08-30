# Plugin One Pager

In order to support different kinds of generative A.I models, we need to support a plugin system
that provides the necessary details for users to be able to register and use their own models.

## Motivations

As a shell and generative A.I user, I may not want to just use one model, but many and I want to be
able to register these additional models as easy as possible.

## Goals

- Create a plugin system that allows for community to contribute their plugins for specific models
- Have all models work with the same basic model management commands
    - `register`
    - `get`
    - `unregister`
    - etc
- Have a way for user to install various plugins for models
- Support plugins that are not open source that may contain proprietary code and information
- provide extensibility for plugins to add subcommands to the executable for customization of their specific models.

## 



