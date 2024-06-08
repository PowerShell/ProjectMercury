# Ollama Plugin

This agent is used to interact with a language model running locally by utilizing the Ollama API. Before using
this agent you need to have Ollama installed and running.

## Pre-requisites to using the agent

- Install [Ollama](https://github.com/ollama/ollama) 
- Install a [Ollama model](https://github.com/ollama/ollama?tab=readme-ov-file#model-library), we
  suggest using the `phi3` model as it is set as the default model in the code
- [Start the Ollama API server](https://github.com/ollama/ollama?tab=readme-ov-file#start-ollama)

## Configuration

Currently to change the model you will need to modify the query in the code in the
`OllamaChatService` class. The default model is `phi3`.

The default endpoint is `http://localhost:11434/api/generate` with `11434` being the default port. This can be changed in the code
and eventually will be added to a configuration file.