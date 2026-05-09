using AxiomFlow.Core.Models;

namespace AxiomFlow.Core.Middleware;

/// <summary>
/// Exception thrown when the AxiomInterceptor blocks a response due to validation failures.
/// Contains the list of failed validation results for downstream handling.
/// </summary>
public sealed class AxiomValidationException : Exception
{
    /// <summary>
    /// The validation results that caused the block.
    /// </summary>
    public IReadOnlyList<ValidationResult> FailedResults { get; }

    public AxiomValidationException(string message, IReadOnlyList<ValidationResult> failedResults)
        : base(message)
    {
        FailedResults = failedResults ?? throw new ArgumentNullException(nameof(failedResults));
    }

    public AxiomValidationException(string message, IReadOnlyList<ValidationResult> failedResults, Exception innerException)
        : base(message, innerException)
    {
        FailedResults = failedResults ?? throw new ArgumentNullException(nameof(failedResults));
    }
}
