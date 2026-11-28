// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using Microsoft.Data.SqlClient;
using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Workspace;

/// <summary>
/// Opens the one connection an avatar batch uses for profile authorization and the image query.
/// </summary>
/// <remarks>The member-detail gateway keeps its own connection. It does not create <c>#AvatarBatchGraphEdges</c>.</remarks>
internal interface IAvatarBatchGraphSessions
{
	/// <summary>Opens the shared connection and records its open time when a profile is present.</summary>
	/// <param name="profile">The first-page profile. Null records nothing.</param>
	/// <param name="cancellationToken">Cancels opening the connection.</param>
	/// <returns>The open session. The caller disposes it, including after a later failure.</returns>
	Task<IAvatarBatchGraphSession> OpenAsync(IWorkspaceGatewayTiming? profile, CancellationToken cancellationToken);
}

/// <summary>
/// One open avatar-batch connection. Profile authorization finishes before the image query.
/// </summary>
/// <remarks>
/// Dispose drops the caller-owned temporary tables and then releases the connection.
/// A profile or graph-copy failure leaves the image query uncalled. Dispose still runs.
/// </remarks>
internal interface IAvatarBatchGraphSession : IAsyncDisposable
{
	/// <summary>
	/// Creates an empty graph-copy table, runs the profile procedure, and reads every result set.
	/// </summary>
	/// <param name="email">Account email. The procedure resolves the account from this value.</param>
	/// <param name="objectType">system, group, or place.</param>
	/// <param name="anchorName">The anchor name from the occurrence.</param>
	/// <param name="branch">The branch derived from the occurrence.</param>
	/// <param name="profile">The first-page profile. Null records nothing.</param>
	/// <param name="cancellationToken">Cancels the table creation or the procedure.</param>
	/// <returns>The six-set bundle. The graph copy is complete when this task completes.</returns>
	Task<AnchorProfileBundle> ReadProfilesAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		IWorkspaceGatewayTiming? profile,
		CancellationToken cancellationToken);

	/// <summary>Reads images for the retention misses from the copied graph.</summary>
	/// <param name="accountId">The account already authorized by the profile procedure. This query does not build a graph from it.</param>
	/// <param name="memberIds">Authorized page members the context does not already hold, in preview order.</param>
	/// <param name="profile">The first-page profile. Null records nothing. The image bytes are unchanged.</param>
	/// <param name="cancellationToken">Cancels the image query. Cancellation is not a missing image.</param>
	/// <returns>Images the query found, keyed by member id. A missing key means that member has no image.</returns>
	Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadImagesAsync(
		Guid accountId,
		IReadOnlyList<Guid> memberIds,
		WorkspaceAvatarBatchProfile? profile,
		CancellationToken cancellationToken);
}

/// <summary>The SQL implementation of <see cref="IAvatarBatchGraphSessions"/>.</summary>
internal sealed class SqlAvatarBatchGraphSessions(IConfiguration configuration) : IAvatarBatchGraphSessions
{
	/// <inheritdoc />
	public async Task<IAvatarBatchGraphSession> OpenAsync(
		IWorkspaceGatewayTiming? profile,
		CancellationToken cancellationToken)
	{
		var connectionString = configuration.GetConnectionString(Globals.connectionString);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new WorkspaceOpeningException("The PluralBridge connection string is not configured.");
		}

		var connection = new SqlConnection(connectionString);
		try
		{
			await MeasureAsync(profile, static (timing, elapsed) => timing.SetProfileConnectionOpen(elapsed), async () =>
			{
				await connection.OpenAsync(cancellationToken);
			});
			return new SqlAvatarBatchGraphSession(connection);
		}
		catch
		{
			await connection.DisposeAsync();
			throw;
		}
	}

	/// <summary>Runs one stage. A null profile performs the work and stores no duration.</summary>
	/// <param name="profile">The profile receiving the duration. Null stores nothing.</param>
	/// <param name="record">Writes the elapsed milliseconds onto the profile.</param>
	/// <param name="action">The stage work.</param>
	/// <returns>The task completes when the stage finishes.</returns>
	private static async Task MeasureAsync(
		IWorkspaceGatewayTiming? profile,
		Action<IWorkspaceGatewayTiming, long> record,
		Func<Task> action)
	{
		if (profile == null)
		{
			await action();
			return;
		}

		var watch = Stopwatch.StartNew();
		try
		{
			await action();
		}
		finally
		{
			watch.Stop();
			record(profile, watch.ElapsedMilliseconds);
		}
	}
}

/// <summary>
/// Profile authorization and the image query on one connection.
/// </summary>
/// <remarks>
/// The empty copy table is created before the procedure. The procedure copies <c>#GraphEdges</c> after its result sets.
/// This session does not call the topology seed, walker, or graph expansion itself.
/// </remarks>
internal sealed class SqlAvatarBatchGraphSession(SqlConnection connection) : IAvatarBatchGraphSession
{
	private bool _disposed;

	/// <inheritdoc />
	public async Task<AnchorProfileBundle> ReadProfilesAsync(
		string email,
		string objectType,
		string anchorName,
		string branch,
		IWorkspaceGatewayTiming? profile,
		CancellationToken cancellationToken)
	{
		var watch = profile == null ? null : Stopwatch.StartNew();
		var capture = profile == null ? null : new AnchorProfileReadCapture(AnchorProfileReadCapture.AvatarCaller);
		try
		{
			if (profile == null)
			{
				await AnchorMemberProfileSave.CreateAvatarBatchGraphTableAsync(connection, cancellationToken);
			}
			else
			{
				var table = Stopwatch.StartNew();
				await AnchorMemberProfileSave.CreateAvatarBatchGraphTableAsync(connection, cancellationToken);
				table.Stop();
				profile.SetGraphCopyTable(table.ElapsedMilliseconds);
			}

			return await AnchorMemberProfileReader.ExecuteAsync(
				connection,
				email,
				objectType,
				anchorName,
				branch,
				cancellationToken,
				capture);
		}
		finally
		{
			if (watch != null && profile != null)
			{
				watch.Stop();
				profile.SetProfileAuthorization(watch.ElapsedMilliseconds);
			}

			if (capture != null)
			{
				profile?.SetProfileRead(capture);
			}
		}
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadImagesAsync(
		Guid accountId,
		IReadOnlyList<Guid> memberIds,
		WorkspaceAvatarBatchProfile? profile,
		CancellationToken cancellationToken)
	{
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		try
		{
			return await AnchorMemberProfileSave.LoadAvatarsAsync(
				useDatabase: true,
				accountId,
				memberIds,
				cancellationToken,
				connection,
				timing);
		}
		finally
		{
			// The profile connection is already recorded. The graph build is inside profile authorization.
			profile?.RecordSharedRead(null, timing);
		}
	}

	/// <summary>Drops the caller-owned tables and releases the connection.</summary>
	/// <returns>The task completes when the connection is released.</returns>
	/// <remarks>A drop failure does not replace the request exception. The connection is still disposed.</remarks>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		try
		{
			await AnchorMemberProfileSave.DropAvatarBatchCallerTablesAsync(connection);
		}
		catch (Exception)
		{
			// The request exception is the one the caller handles. Releasing the connection still happens below.
		}

		try
		{
			await connection.DisposeAsync();
		}
		catch (Exception)
		{
			// A dispose failure must not replace a profile or image exception already in flight.
		}
	}
}
