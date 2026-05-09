namespace AxiomFlow.Persistence;

/// <summary>
/// Configuration options for the Cosmos DB Semantic Ledger provider.
/// Supports both Cosmos DB Emulator and live Azure instances.
/// </summary>
public sealed class CosmosLedgerOptions
{
    /// <summary>
    /// Configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "AxiomFlow:CosmosLedger";

    /// <summary>
    /// Cosmos DB connection string.
    /// For emulator: "AccountEndpoint=https://localhost:8081/;AccountKey=..."
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Database name. Created automatically if it doesn't exist.
    /// </summary>
    public string DatabaseName { get; set; } = "axiom-flow";

    /// <summary>
    /// Container name for validation artifacts.
    /// </summary>
    public string ContainerName { get; set; } = "validation-artifacts";

    /// <summary>
    /// Partition key path. Must match the property used in ValidationArtifact.
    /// </summary>
    public string PartitionKeyPath { get; set; } = "/sessionId";

    /// <summary>
    /// Time-to-live in seconds for validation artifacts. -1 = no expiry.
    /// Default: 90 days (7,776,000 seconds).
    /// </summary>
    public int DefaultTimeToLiveSeconds { get; set; } = 7_776_000;

    /// <summary>
    /// Provisioned throughput in RU/s. Set to -1 for serverless.
    /// </summary>
    public int ThroughputRUs { get; set; } = 400;
}
