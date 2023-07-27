#!/bin/bash

command_str='bind -x "\"\eOR\":\"./ai.exe --restore\""'

echo "$command_str" >> "$HOME/.inputrc"

echo "Shell Copilot registered for F3. Please restart your terminal or run 'source ~/.inputrc' to apply the changes."
