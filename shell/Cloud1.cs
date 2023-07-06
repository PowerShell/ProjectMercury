using System.Security;

namespace shell;

public class Cloud
{
    private readonly Dictionary<string, AzOpenAIEndpoint> _endpoints;
    private readonly Dictionary<string, AIModel> _models;

    public Cloud()
    {
        // Load from configuration files?
        _endpoints = new();
        _models = new();
    }
}

public class AzOpenAIEndpoint
{
    private readonly Uri _endpointURI;
    private readonly List<AzOpenAIDeployment> _deployments;
    private SecureString? _userKey;

    public AzOpenAIEndpoint(string uri, SecureString? userKey = null)
    {
        _endpointURI = new Uri(uri);
        _deployments = new List<AzOpenAIDeployment>();
        _userKey = userKey;
    }

    public SecureString? UserKey { get; set; }
}

public class AzOpenAIDeployment
{
    private readonly string _id;
    private readonly int _maxToken;

    public AzOpenAIDeployment(string id, int maxToken)
    {
        _id = id;
        _maxToken = maxToken;
    }

    public string Id => _id;
    public int MaxToken => _maxToken;
}

public class AIModel
{
    private readonly string _name;
    private readonly string _description;
    private readonly AzOpenAIEndpoint _endpoint;
    private readonly string _deploymentId;
    private readonly string _systemPrompt;

    public AIModel(string name, string description, AzOpenAIEndpoint endpoint, string deploymentId, string prompt)
    {
        _name = name;
        _description = description;
        _endpoint = endpoint;
        _deploymentId = deploymentId;
        _systemPrompt = prompt;
    }

    public string Name => _name;
    public string Description => _description;
    public AzOpenAIEndpoint Endpoint => _endpoint;
    public string DeploymentId => _deploymentId;
    public string SystemPrompt => _systemPrompt;
}
