# Ollama Plugin

This agent is used to communicate with the Ollama API with a locally running language model. Before utilizing
this agent you need to have Ollama installed and running.

## Pre-requisites to using the agent

- Install [Ollama](https://github.com/ollama/ollama) 
- Install a [Ollama model](https://github.com/ollama/ollama?tab=readme-ov-file#model-library), we
  suggest using the `phi3` model as it is set as the default model in the code
- [Start the Ollama API server](https://github.com/ollama/ollama?tab=readme-ov-file#start-ollama)

## Configuration

Currently to change the model you will need to modify the query in the code in the
`OllamaChatService` class. The default model is `phi3`.

The endpoint is `http://localhost`and the default port is `11434`. This can be changed in the code
and eventually will be added to a configuration file.