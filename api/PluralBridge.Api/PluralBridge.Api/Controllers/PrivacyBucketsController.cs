using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PluralBridge.Api.Controllers;

/// <summary>
/// Provides the read-only privacy buckets endpoint for the Phase 2B proof surface.
/// Privacy buckets are returned for a specific imported PluralBridge system.
/// </summary>
[ApiController]
[Route(Globals.privacyBucketsRoute)]
public sealed class PrivacyBucketsController(IConfiguration configuration) : ControllerBase
{
	/// <summary>
	/// Returns all privacy buckets for the requested system from the validated proof database.
	/// The response includes count metadata and keeps write capability explicitly disabled.
	/// </summary>
	/// <param name="systemId">The PluralBridge system identifier used to scope the privacy bucket query.</param>
	/// <returns>
	/// HTTP 200 with privacy bucket rows, total count, and read-only capability metadata.
	/// </returns>
	[HttpGet]
	public async Task<IActionResult> Get(Guid systemId)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			return Problem(
				title: Globals.missingConnectionString,
				detail: Globals.missingConnStringDetail,
				statusCode: StatusCodes.Status500InternalServerError);
		}

		await using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();

		var accessContext = await AccessContextHelper.ResolveCurrentAccessAsync(connection);

		if (accessContext is null)
		{
			return Unauthorized(new
			{
				api = Globals.apiName,
				phase = Globals.projectPhase,
				endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.privacyBucketsEndpointSegment}",
				canWrite = false,
				systemId,
				error = Globals.cantResolveAccess
			});
		}

		if (!AccessContextHelper.IsAuthorizedForCurrentSystem(accessContext) || accessContext.CurrentSystem.SystemId != systemId)
		{
			return Forbid();
		}

		var privacyBuckets = await ReadPrivacyBucketsAsync(
			connection,
			accessContext.CurrentSystem.SystemId);

		return Ok(new
		{
			api = Globals.apiName,
			phase = Globals.projectPhase,
			endpoint = $"{Globals.systemsEndpointRoot}/{systemId}/{Globals.privacyBucketsEndpointSegment}",
			canWrite = false,
			systemId = accessContext.CurrentSystem.SystemId,
			count = privacyBuckets.Count,
			privacyBuckets
		});
	}

	/// <summary>
	/// Reads privacy bucket rows for one system from the validated proof database.
	/// The selected fields expose imported privacy grouping metadata without exposing raw source payloads.
	/// </summary>
	/// <param name="connection">An open SQL Server connection to the PluralBridge proof database.</param>
	/// <param name="systemId">The PluralBridge system identifier used to scope the privacy bucket query.</param>
	/// <returns>The privacy bucket rows ordered by rank text, bucket name, and bucket identifier.</returns>
	private static async Task<List<PrivacyBucket>> ReadPrivacyBucketsAsync(SqlConnection connection, Guid systemId)
	{
		const string sql = """
		                   SELECT
		                       PrivacyBucketId,
		                       SystemId,
		                       BucketName,
		                       Description,
		                       Color,
		                       Icon,
		                       RankText,
		                       ImportedAtUtc,
		                       CreatedAtUtc,
		                       UpdatedAtUtc
		                   FROM dbo.pb_privacy_buckets
		                   WHERE SystemId = @SystemId
		                   ORDER BY RankText, BucketName, PrivacyBucketId;
		                   """;

		var privacyBuckets = new List<PrivacyBucket>();

		await using var command = new SqlCommand(sql, connection);
		command.Parameters.AddWithValue("@SystemId", systemId);

		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			privacyBuckets.Add(new PrivacyBucket(
				reader.GetGuid(0),
				reader.GetGuid(1),
				reader.GetString(2),
				reader.IsDBNull(3) ? null : reader.GetString(3),
				reader.IsDBNull(4) ? null : reader.GetString(4),
				reader.IsDBNull(5) ? null : reader.GetString(5),
				reader.IsDBNull(6) ? null : reader.GetString(6),
				reader.GetDateTime(7),
				reader.GetDateTime(8),
				reader.IsDBNull(9) ? null : reader.GetDateTime(9)));
		}

		return privacyBuckets;
	}

	private sealed record PrivacyBucket(
		Guid PrivacyBucketId,
		Guid SystemId,
		string BucketName,
		string? Description,
		string? Color,
		string? Icon,
		string? RankText,
		DateTime ImportedAtUtc,
		DateTime CreatedAtUtc,
		DateTime? UpdatedAtUtc);
}