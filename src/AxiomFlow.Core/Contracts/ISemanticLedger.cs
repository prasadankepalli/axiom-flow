using AxiomFlow.Core.Models;

namespace AxiomFlow.Core.Contracts;

/// <summary>
/// Abstraction for the Semantic Ledger — the persistence layer that stores
/// validation artifacts for audit, replay, and observability.
/// </summary>
public interface ISemanticLedger
{
    /// <summary>
    /// Records a validation artifact to the ledger.
    /// </summary>
    /// <param name="artifact">The validation artifact to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(ValidationArtifact artifact, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all validation artifacts for a given session.
    /// </summary>
    /// <param name="sessionId">The session/conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of validation artifacts for the session.</returns>
    Task<IReadOnlyList<ValidationArtifact>> GetBySessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific validation artifact by its ID and session.
    /// </summary>
    /// <param name="id">The artifact document ID.</param>
    /// <param name="sessionId">The session ID (partition key).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artifact, or null if not found.</returns>
    Task<ValidationArtifact?> GetByIdAsync(
        string id,
        string sessionId,
        CancellationToken cancellationToken = default);
}
