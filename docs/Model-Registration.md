# Model Registration Experience

## Overview

In this doc, a `model` means everything needed to connect to a AI chat session.
This includes:

- Model Name
- Model Description
- HTTPS endpoint
- User API Key
- Trust level
- System Prompt
- Examples

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
ai register --name "My Model" --description "My Model Description" --endpoint "https://my-model.com" --key "my-model-key" --prompt "My Model Prompt"
```

Here, the mandatory parameters are:

- `--name` - The name of the model
- `--endpoint` - The HTTPS endpoint of the model

Optional pareameters are:

- `--description` - The description of the model
- `--prompt` - The system prompt for the model. Default is no prompt is sent as the endpoint itself may provide a prompt.
- `--key` - The API key for the model (some models may allow for anonymous access)
- `--trust` - The trust level of the model: `public` or `private`. Default is `public`.

#### Model Owner Registration Changes

The developer of the model may want to change the model registration information.

```powershell
ai set --name "My Model" --description "My Model Description"
```

Here, the mandatory parameters are:

- `--name` - The name of the model

Optional pareameters are:

- `--description` - The description of the model
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
- `--file` - The file to export the model registration information to

#### Model JSON Format

The model JSON format is as follows:

```json
{
    "name": "My Model",
    "description": "My Model Description",
    "endpoint": "https://my-model.com",
    "prompt": "My Model Prompt"
}
```

> [Note] It might make sense to have a flag indicating if a user key is required.

### User Model Import

The user of the model may want to import the model registration information from a file.

```powershell
ai import --file "my-model.json" --name "Business Model" --trust private --key "my-user-key"
```

Here the user is importing the model registration information from the file `my-model.json` and registering it as a private model
with the name `Business Model`.

Users can also use the `set` command to change the model registration information.

### User Model Enumeration

The user of the model may want to see the models that are available to them.

```powershell
ai list
```

### User Model Selection

The user of the model may want to select a model to use.

```powershell
ai select --name "Business Model"
```
