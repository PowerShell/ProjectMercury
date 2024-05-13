using System.Reflection;
using System.Runtime.Loader;

namespace AISH.Kernel;

internal class AgentAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _dependencyDir;

    internal AgentAssemblyLoadContext(string name, string dependencyDir)
        : base($"{name.Replace(' ', '.')}-ALC", isCollectible: false)
    {
        if (!Directory.Exists(dependencyDir))
        {
            throw new ArgumentException($"The agent home directory '{dependencyDir}' doesn't exist.", nameof(dependencyDir));
        }

        // Save the full path to the dependencies directory when creating the context.
        _dependencyDir = dependencyDir;
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        // Create a path to the assembly in the dependencies directory.
        string path = Path.Combine(_dependencyDir, $"{assemblyName.Name}.dll");

        if (File.Exists(path))
        {
            // If the assembly exists in our dependency directory, then load it into this load context.
            return LoadFromAssemblyPath(path);
        }

        // Otherwise we will depend on the default load context to resolve the request.
        return null;
    }
}
