
command_str='bind -x "\"\eOR\":\"dotnet ai.dll --restore\""'

echo "$command_str" >> "$HOME/.inputrc"

echo "Shell Copilot registered for F3. Please restart your terminal or run 'source ~/.inputrc' to apply the changes."
