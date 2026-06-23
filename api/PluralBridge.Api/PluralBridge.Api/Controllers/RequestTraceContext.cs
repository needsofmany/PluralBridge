using Microsoft.Extensions.Logging;

namespace PluralBridge.Api.Controllers
{
	internal sealed class RequestTraceContext
	{
		internal const string Level1TraceEvent = "PB_LEVEL1_TRACE";

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="traceId">The request trace identifier, or null to create one.</param>
		/// <param name="correlationId">The request correlation identifier, or null to reuse TraceId.</param>
		/// <param name="requestStartedAtUtc">The request start timestamp, or null to use the current UTC time.</param>
		private RequestTraceContext(
			string traceId,
			string correlationId,
			DateTimeOffset requestStartedAtUtc)
		{
			TraceId = traceId;
			CorrelationId = correlationId;
			RequestStartedAtUtc = requestStartedAtUtc;
		}

		internal string TraceId { get; }

		internal string CorrelationId { get; }

		internal DateTimeOffset RequestStartedAtUtc { get; }

		/// <summary>
		/// Creates a safe Level 1 diagnostic trace context for the current request spine.
		/// </summary>
		/// <param name="traceId">The request trace identifier, or null to create one.</param>
		/// <param name="correlationId">The request correlation identifier, or null to reuse TraceId.</param>
		/// <param name="requestStartedAtUtc">The request start timestamp, or null to use the current UTC time.</param>
		/// <returns>A safe Level 1 diagnostic trace context.</returns>
		internal static RequestTraceContext Create(
			string? traceId = null,
			string? correlationId = null,
			DateTimeOffset? requestStartedAtUtc = null)
		{
			var resolvedTraceId = string.IsNullOrWhiteSpace(traceId)
				? Guid.NewGuid().ToString("N")
				: traceId.Trim();

			var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
				? resolvedTraceId
				: correlationId.Trim();

			return new RequestTraceContext(
				resolvedTraceId,
				resolvedCorrelationId,
				requestStartedAtUtc ?? DateTimeOffset.UtcNow);
		}

		/// <summary>
		/// Emits one safe Level 1 diagnostic trace fact for a request-spine stage.
		/// </summary>
		/// <param name="logger">The logger receiving the trace fact.</param>
		/// <param name="stageName">The safe request-spine stage name.</param>
		/// <param name="outcome">The safe stage outcome.</param>
		/// <param name="elapsed">The optional stage elapsed time.</param>
		internal void LogStage(
			ILogger logger,
			string stageName,
			string outcome,
			TimeSpan? elapsed = null)
		{
			ArgumentNullException.ThrowIfNull(logger);

			if (string.IsNullOrWhiteSpace(stageName))
			{
				throw new ArgumentException("Stage name is required.", nameof(stageName));
			}

			if (string.IsNullOrWhiteSpace(outcome))
			{
				throw new ArgumentException("Outcome is required.", nameof(outcome));
			}

			logger.LogInformation(
				"{TraceEvent} TraceId={TraceId} CorrelationId={CorrelationId} Stage={StageName} Outcome={Outcome} ElapsedMs={ElapsedMilliseconds}",
				Level1TraceEvent,
				TraceId,
				CorrelationId,
				stageName.Trim(),
				outcome.Trim(),
				elapsed?.TotalMilliseconds);
		}
	}
}