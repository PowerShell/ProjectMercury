# demo.txt for `ai` commands

> Assuming the executable will be named `ai`.

This document focuses on the `ai` command when it's used **non-interactively**.

```
ai --help

  ai [prefix-term]

  options
    -h, --help
    --version

  sub-commands
    endpoint
    model
```

When a prefix-term is specified, `ai` makes a one-off request to the AI endpoint,
and then display the response.
`ai` should read from standard input and send the input content along with the specified prefix-term,
so that the user can feed anything to `ai` as the context content for the prefix-term.

When used with no prefix-term specified, `ai` runs in interactive mode.

## demo.txt for `ai endpoint`

This demo.txt focus on the `ai endpoint` sub command.

> NOTE: maybe we also want to allow users to make these operations interactively in our mini-shell window.

Command syntax is as follows

```
> ai endpoint --help

  new       Create a new endpoint for use by models
  list      List available endpoints
  show      Show details of the specified endpoint
  update    Update properties of the endpoint
  remove    Remove the specified endpoint
```

### `ai endpoint new`

```
> ai endpoint new --help

  -n, --name               Name of the endpoint
      --url                URL of the endpoint

> ai endpoint new -n pwsh-public

Enter the endpoint URL: https://powershell-openai.openai.azure.com
Enter the API key: *******************

Enter the deployment id: gpt-35-turbo
Enter the token limit: 4096
Want to enter more deployments?
  > yes
    no

Enter the deployment id: gpt-4
Enter the token limit: 8192
Want to enter more deployments?
    yes
  > no
```

### `ai endpoint list`

```
> ai endpoint list

Name                Base URL
------------        ------------
pwsh-public         https://powershell-openai.openai.azure.com
```

### `ai endpoint show`

```
> ai endpoint show pwsh-public

Name        : pwsh-public
URL         : https://powershell-openai.openai.azure.com
Deployments : gpt-35-turbo (token_limit: 4096), gpt-4 (token_limit: 8196)
Api-key     : pjt...HnkM
```

### `ai endpoint update`

```
> ai endpoint update --help

  -n, --name           Update the name of the specified endpoint
      --url            Update the URL of the specified endpoint
  -d, --deployments    Update the deployments of the specified endpoint
  -k, --api-key        Update the api key of the specified endpoint

> ai endpoint update -d

Choose your operation on the deployments:
  > add
    remove
    done
Enter the deployment id: gpt-4-32k
Enter the token limit: 32000
New deployment added.

Choose your operation on the deployments:
    add
  > remove
    done
Enter the deployment id to remove [gpt-35-turbo/gpt-4/gpt-4-32k]: non-exist
That's not a valid input, try again
Enter the deployment id to remove [gpt-35-turbo/gpt-4/gpt-4-32k]: gpt-35-turbo
Deployment 'gpt-35-turbo' removed.

Choose your operation on the deployments:
    add
    remove
  > done
```

### `ai endpoint remove`

```
> ai endpoint remove --help

ai endpoint remove <name>

  -f, --force       Remove the specified endpoint and the models using it

> ai endpoint remove pwsh-public
The endpoint 'pwsh-public' cannot be removed because it's used by the following models: model-a, model-b

> ai endpoint remove pwsh-public -f
Endpoint 'pwsh-public' removed

> ai endpoint remove
Select the endpoint to be removed:
    endpoint-a
  > endpoint-b
    pwsh-public
Endpoint 'endpoint-b' removed
```

## demo.txt for `ai model`

This demo.txt focus on the `ai model` sub command.

> NOTE: maybe we also want to allow users to make these operations interactively in our mini-shell window.

Command syntax is as follows

```
> ai model --help

  new       Create a new model for use
  use       Use the specified model as the current active
  list      List models that are registered
  show      Show details of the specified model
  remove    Remove the specified model
```

### `ai model new`

```
> ai model new --help

  -n, --name               Name of the model
  -d, --description        Description of the model
  -e, --endpoint           Endpoint URL to use for this model
  -m, --deployment         The deployment id
  -p, --system-prompt      The system prompt for the model.
                           Take prompt text as a string or a file path that contains the prompt text.

## User can choose to specify all those flags, or they can choose to fill in all interactively.
> ai model new -n bash

Enter description of the model: Assistant with expertise around Unix utilities and bash scripts
Choose the endpoint:
    https://some-thing-fun.openai.azure.com
  > https://powershell-openai.openai.azure.com

Choose the deployment id from the selected endpoint:
    gpt-35-turbo
  > gpt-4
    gpt-4-32k

Enter the system prompt to be used (text or path-to-a-file): E:\prompts\bash-system-prompt.txt
```

### `ai model list`

`ai model list` should support output JSON format too, in which case each model item will have an additional property `Active`.
When the JSON format is not in use, the `Active` column is not need when rendering in console.

```
> ai model list

Name                Description
------------        ------------
bash (active)       Assistant with expertise around Unix utilities and bash scripts
pwsh                Assistant specialized in PowerShell scripting language
spec-helper         Assistant that helps generate content for a technical specification.
```

### `ai model show`

```
> ai model show xxx

Name          : bash
Description   : Assistant with expertise around Unix utilities and bash scripts
Endpoint      : https://powershell-openai.openai.azure.com
Deployment    : gpt-35-turbo
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
