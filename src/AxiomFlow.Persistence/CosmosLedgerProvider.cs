using AxiomFlow.Core.Contracts;
using AxiomFlow.Core.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace AxiomFlow.Persistence;

/// <summary>
/// Cosmos DB implementation of the Semantic Ledger.
/// Stores ValidationArtifacts partitioned by sessionId for efficient session-scoped queries.
/// Supports automatic database/container creation for development scenarios (emulator).
/// </summary>
public sealed class CosmosLedgerProvider : ISemanticLedger, IAsyncDisposable
{
    private readonly CosmosClient _client;
    private readonly CosmosLedgerOptions _options;
    private readonly ILogger<CosmosLedgerProvider> _logger;
    private Container? _container;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public CosmosLedgerProvider(
        IOptions<CosmosLedgerOptions> options,
        ILogger<CosmosLedgerProvider> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Gateway // Gateway mode works with emulator
        };

        _client = new CosmosClient(_options.ConnectionString, clientOptions);
    }

    /// <inheritdoc />
    public async Task RecordAsync(ValidationArtifact artifact, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        _logger.LogDebug(
            "[SemanticLedger] Recording artifact {Id} for session {Session}, invariant '{Invariant}'",
            artifact.Id, artifact.SessionId, artifact.InvariantName);

        await container.CreateItemAsync(
            artifact,
            new PartitionKey(artifact.SessionId),
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "[SemanticLedger] Artifact {Id} recorded. OverallPass={Pass}",
            artifact.Id, artifact.OverallPass);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ValidationArtifact>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.sessionId = @sessionId ORDER BY c.createdAt DESC")
            .WithParameter("@sessionId", sessionId);

        var results = new List<ValidationArtifact>();
        using var iterator = container.GetItemQueryIterator<ValidationArtifact>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(sessionId) });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response.Resource);
        }

        return results.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ValidationArtifact?> GetByIdAsync(
        string id,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken);

        try
        {
            var response = await container.ReadItemAsync<ValidationArtifact>(
                id,
                new PartitionKey(sessionId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_initialized && _container != null)
            return _container;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && _container != null)
                return _container;

            _logger.LogInformation("[SemanticLedger] Initializing Cosmos DB: {Database}/{Container}",
                _options.DatabaseName, _options.ContainerName);

            var database = await _client.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                cancellationToken: cancellationToken);

            var containerProperties = new ContainerProperties(_options.ContainerName, _options.PartitionKeyPath)
            {
                DefaultTimeToLive = _options.DefaultTimeToLiveSeconds
            };

            var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput: _options.ThroughputRUs > 0 ? _options.ThroughputRUs : null,
                cancellationToken: cancellationToken);

            _container = containerResponse.Container;
            _initialized = true;

            _logger.LogInformation("[SemanticLedger] Cosmos DB initialized successfully");
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _initLock.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
