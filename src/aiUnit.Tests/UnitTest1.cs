namespace Microsoft.PowerShell.Copilot;

public class ModelProgramTests
{
    [Fact]
    public void Register_NewModel_NoReturn()
    {
        ModelFunctions.addModel("test", "test", "https://powershell-openai.openai.azure.com", "test", "gpt4", "gpt4", "public", "test");
    }
}