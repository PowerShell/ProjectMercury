namespace aiUnit.Tests;

public class ModelProgramTests
{
    [Fact]
    public void Register_NewModel_NoReturn()
    {
        Microsoft.PowerShell.Copilot.ModelFunctions.addModel("test", "test", "test", "test", "test", "test", "test", "test");
    }
}