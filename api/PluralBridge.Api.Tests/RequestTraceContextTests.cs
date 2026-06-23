using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

public sealed class RequestTraceContextTests
{
	[Fact]
	public void Create_WithTraceIdAndCorrelationId_KeepsBothValues()
	{
		var requestTrace = RequestTraceContext.Create(
			"trace-123",
			"correlation-456",
			new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero));

		Assert.Equal("trace-123", requestTrace.TraceId);
		Assert.Equal("correlation-456", requestTrace.CorrelationId);
	}

	[Fact]
	public void Create_WithoutCorrelationId_ReusesTraceId()
	{
		var requestTrace = RequestTraceContext.Create(
			"trace-123",
			null,
			new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero));

		Assert.Equal("trace-123", requestTrace.TraceId);
		Assert.Equal("trace-123", requestTrace.CorrelationId);
	}

	[Fact]
	public void LogStage_EmitsTraceIdCorrelationIdAndStageName()
	{
		var logger = new CapturingLogger();
		var requestTrace = RequestTraceContext.Create(
			"trace-123",
			"correlation-456",
			new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero));

		requestTrace.LogStage(
			logger,
			"authorization_check",
			"allowed",
			TimeSpan.FromMilliseconds(12));

		var message = Assert.Single(logger.Messages);

		Assert.Contains(RequestTraceContext.Level1TraceEvent, message);
		Assert.Contains("TraceId=trace-123", message);
		Assert.Contains("CorrelationId=correlation-456", message);
		Assert.Contains("Stage=authorization_check", message);
		Assert.Contains("Outcome=allowed", message);
	}

	[Fact]
	public void LogStage_DoesNotIncludeSensitiveFields()
	{
		var logger = new CapturingLogger();
		var requestTrace = RequestTraceContext.Create(
			"trace-123",
			"correlation-456",
			new DateTimeOffset(2026, 6, 21, 0, 0, 0, TimeSpan.Zero));

		requestTrace.LogStage(
			logger,
			"data_access",
			"completed",
			TimeSpan.FromMilliseconds(12));

		var message = Assert.Single(logger.Messages);

		Assert.DoesNotContain("Email", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("DisplayName", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("MemberName", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Note", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("CustomField", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Avatar", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("RawPayload", message, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("Exception", message, StringComparison.OrdinalIgnoreCase);
	}

	private sealed class CapturingLogger : ILogger
	{
		internal List<string> Messages { get; } = [];

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return true;
		}

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			Messages.Add(formatter(state, exception));
		}
	}
}
