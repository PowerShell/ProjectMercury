## demo.txt for `ai model`

Assuming the executable will be named `ai`.
This demo.txt focus on the `ai model` sub command.

> NOTE: maybe we also want to allow users to make these operations interactively in our mini-shell window.

Command syntax is as follows

```
> ai model --help

  add       Add a new model for use
  use       Use the specified model as the current active
  list      List models that are registered
  show      Show details of the specified model
  remove    Remove the specified model
```

### `ai model add`

```
> ai model add --help

  -n, --Name               Name of the model
  -d, --Description        Description of the model
  -e, --Endpoint           Endpoint URL to use for this model
  -m, --Deployment         The deployment id
  -p, --SystemPrompt       The system prompt for the model.
                           Take prompt text as a string or a file path that contains the prompt text.
```

### `ai list`

`ai list` should support output JSON format too, in which case each model item will have an additional property `Active`.
When the JSON format is not in use, the `Active` column is not need when rendering in console.

```
> ai model list

Name                Description
bash (active)       Assistant with expertise around Unix utilities and bash scripts
yyy                 yyyyyyyy ...
zzz                 zzzzzzzz ...
```

### `ai model show`

```
> ai model show xxx

Name          : bash
Description   : Assistant with expertise around Unix utilities and bash scripts
Endpoint      : powershell-openai.openai.azure.com
Deployment    : gpt4, gpt4-32k, gpt-35-turbo
System Prompt : You are a helpful assistant with expertise in the `bash` shell and Linux command-line utilities.
                The following are some rules you need to follow when responding to a request:
                 1. ...
                 2. ...
                 ...
```

### `ai model use`

When current active model is changed, we need to reset the chat history.

```
> ai model use --help
ai model use <name>

> ai model use bash
Current active model: bash
```

### `ai model remove`

To remove the current active model, one has to first set another model as active.

```
> ai model remove --help
ai model remove <name>

> ai model remove bash
The model 'bash' removed.
```
