# Model Registration Experience

## Overview

In this doc, a `model` means everything needed to connect to a AI chat session.
This includes:

- Model Name
- Model Description
- Token Limit
- HTTPS Endpoint
- Deployment Name
- OpenAI Model Name
- User API Key
- Trust level
- System Prompt

Users would register multiple models on the same system to be used dynamically based on the user's scenario.
A separate model could be used to infer the user's intent based on the user prompt and choose the appropriate model to use,
but that concept is outside the scope of this doc and would not be user registered.

## Model Registration

There are two different personas that can register a model:

- Model Owner (developer)
- Model User (chatbot user)

### Model Owner

The model owner is the developer who is creating a model for other users to use.
They may be actively developing the properties of the model, or they may be done and just want to share it with others.

#### Model Owner Registration

Developer creating a custom model:

```powershell
ai register --name "My Model" --description "My Model Description" --endpoint "https://my-model.com" --deployment "gpt-35-turbo" --key "my-model-key" --prompt "My Model Prompt"
```

Here, the mandatory parameters are:

- `--name` - The name of the model
- `--endpoint` - The HTTPS endpoint of the model
- `--deployment` - The name of the deployment
- `--prompt` - The system prompt for the model

Optional pareameters are:

- `--description` - The description of the model
- `--openai-model` - The name of the OpenAI model used by the deployment. Assume the same as `--deployment` when not specified.
- `--key` - The API key for the model (some models may allow for anonymous access)
- `--token-limit` - The maximum number of total tokens allowed in a chat completion call. Default is 4096.
- `--trust` - The trust level of the model: `public` or `private`. Default is `public`.

For `trust`, `public` means that the model is not trusted as it's shared with other users,
so when sending large amounts of data (not typed, but sent via clipboard or a file via a
command), then the user will be prompted to confirm the action.
A `private` model is trusted, so the user will not be prompted to confirm the action.

>[NOTE] As models get more complicated and aspects of them are closed source,
> it may make sense to eventually have an inproc library plugin model where
> the model developer can provide a DLL that implements the model interface

#### Model Owner Display Registration

Developer displaying the registration information for a model:

```powershell
ai get --name "My Model"
```

Here, the mandatory parameters are:

- `--name` - The name of the model

This results in a YAML output of the model registration information.
Key information is never displayed.

```yaml
name: My Model
description: My Model Description
endpoint: https://my-model.com
deployment: gpt-35-turbo
openai-model: gpt-35-turbo
token-limit: 4096
prompt: My Model Prompt
trust: public
```

#### Model Owner Registration Changes

The developer of the model may want to change the model registration information.

```powershell
ai set --name "My Model" --description "My Model Description"
```

Here, the mandatory parameters are:

- `--name` - The name of the model, this is read-only and cannot be changed

Optional pareameters are:

- `--description` - The description of the model
- `--deployment` - The name of the deployment
- `--openai-model` - The name of the OpenAI model used by the deployment
- `--endpoint` - The HTTPS endpoint of the model
- `--token-limit` - The maximum number of total tokens allowed for a chat completion call
- `--prompt` - The system prompt for the model
- `--trust` - The trust level of the model: `public` or `private`. Default is `public`.
- `--key` - The API key for the model

#### Model Owner Export

The developer of the model may want to export the model registration information for other users to import.

```powershell
ai export --name "My Model" --file "my-model.json"
```

Here, the mandatory parameters are:

- `--name` - The name of the model

Optional pareameters are:

- `--file` - The file to export the model registration information to.  If this is not specified, the model registration information will be printed to STDOUT.
- `--all` - Export all models as a single JSON array.

#### Model JSON Format

The model JSON format is as follows:

```json
{
    "name": "My Model",
    "description": "My Model Description",
    "endpoint": "https://my-model.com",
    "deployment": "My-Deployment",
    "openai-model": "gpt-35-turbo",
    "token-limit": 4096,
    "prompt": "My Model Prompt"
}
```

> [Note] It might make sense to have a flag indicating if the key should be included,
and also a flag indicating if the the trust level should be included.

### User Model Import

The user of the model may want to import the model registration information from a file.

```powershell
## 'my-model.json' contains only 1 model's registration information
ai import --file "my-model.json" --name "Business Model" --trust private --key "my-user-key"
```

Here the user is importing the model registration information from the file `my-model.json` and registering it as a private model
with the name `Business Model`.

Users can also use the `set` command to change the model registration information.

Multiple models may be imported from a single file which contains a single JSON object which is an array of models.

```powershell
## 'my-model.json' contains multiple models' registration information
## Using flags like '--name', '--trust', and '--key' will result in error in this case.
ai import --file "my-model.json"
```

### User Model Unregestration

The user of the model may want to unregister a model.

```powershell
ai unregister --name "Business Model"
```

Here, the mandatory parameters are:

- `--name` - The name of the model

### User Model Enumeration

The user of the model may want to see the models that are available to them.

```powershell
ai list
```

For users, the list will contain only relevant information for users of a model:

```console
name       active  trust   description
----       ------  -----   -----------
PowerShell yes     public  PowerShell expert
Azure              public  Azure expert
Contoso            private Contoso enterprise instance
```

### User Model Selection

The user of the model may want to select a model to use.

```powershell
ai use --name "Business Model"
```

### User Model Selection within AI Chat

The user may want to change models within the AI chat session.

The following commands will be available:

- `get <name>` - Get the model registration information
- `list` - List the models available to the user
- `use <name>` - Select a model to use

We would want tab-completion available for the model names.
