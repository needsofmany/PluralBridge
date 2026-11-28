// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The avatar batch authorizes a member page before it returns images.
/// These calls use the production batch service, the common loader, and the avatar factory. They do not open a database connection.
/// </summary>
public sealed class WorkspaceAvatarBatchTests
{
	/// <summary>
	/// The authorized page returns factory bytes in preview order. A member with no image stays null, and a member the procedure omits is absent.
	/// </summary>
	/// <returns>The task completes when the page matches that order.</returns>
	[Fact]
	public async Task Batch_ReturnsFactoryBytesInPreviewOrder()
	{
		var omitted = Guid.Parse("a1000000-0000-4000-8000-0000000000ee");
		var context = PageContext(
		[
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			omitted,
			AvatarTestDataFactory.BlueMemberId
		]);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds =
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(1, gateway.Reads);
		Assert.Equal(
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			],
			ready.Images.Select(image => image.MemberId).ToArray());
		Assert.Equal(AvatarTestDataFactory.RedPng, ready.Images[0].Bytes);
		Assert.Equal("image/png", ready.Images[0].ContentType);
		Assert.Null(ready.Images[1].Bytes);
		Assert.Null(ready.Images[1].ContentType);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.NotEqual(ready.Images[0].Bytes, ready.Images[2].Bytes);
		Assert.DoesNotContain(ready.Images, image => image.MemberId == omitted);
		Assert.Equal(
			[AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.MemberWithoutAvatarId, AvatarTestDataFactory.BlueMemberId],
			reader.Members);
		Assert.Equal(1, reader.BatchCalls);
		Assert.Equal(reader.Members, Assert.Single(reader.BatchRequests));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.MemberWithoutAvatarId));
		Assert.Same(AvatarTestDataFactory.RedPng, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);
	}

	/// <summary>
	/// A retained image is returned without another read, and the profile procedure still runs first.
	/// </summary>
	/// <returns>The task completes when the second authorized call reuses the entry and a failed call does not return it.</returns>
	[Fact]
	public async Task Batch_ReusesARetainedImageAfterAuthorization()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		var loaded = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.RedMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		var retained = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var first = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var second = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(2, gateway.Reads);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Equal(1, reader.BatchCalls);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], Assert.Single(reader.BatchRequests));
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Same(retained!.Bytes, first.Images[0].Bytes);
		Assert.Same(first.Images[0].Bytes, second.Images[0].Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, second.Images[1].Bytes);

		gateway.Failure = new InvalidOperationException("The profile procedure rejected the anchor.");
		var reads = reader.Members.Count;
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(reads, reader.Members.Count);
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
	}

	/// <summary>
	/// Another account and an invalid occurrence do not receive images or call the reader.
	/// </summary>
	/// <returns>The task completes when both requests are rejected.</returns>
	[Fact]
	public async Task Batch_RejectsAnotherAccountAndAWrongOccurrence()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId]);
		var gateway = new AuthorizingGateway(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"))
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		Assert.IsType<WorkspaceAvatarBatchResult.ContextUnavailable>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(0, gateway.Reads);
		Assert.Empty(reader.Members);

		gateway.AccountId = AvatarTestDataFactory.AccountId;
		var rejected = Assert.IsType<WorkspaceAvatarBatchResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "missing-path", CancellationToken.None));
		Assert.Equal("The selected occurrence is not in this workspace.", rejected.Error);
		Assert.Equal(0, gateway.Reads);
		Assert.Empty(reader.Members);

		var negative = Assert.IsType<WorkspaceAvatarBatchResult.OccurrenceRejected>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, -1));
		Assert.Equal("The member batch skip must be zero or greater.", negative.Error);
		Assert.Equal(0, gateway.Reads);
	}

	/// <summary>
	/// Skip follows the same unique direct-member windows as cards. An empty page still authorizes and does not read images.
	/// </summary>
	/// <returns>The task completes when the second window and the empty window match those rules.</returns>
	[Fact]
	public async Task Batch_PagesUniqueMembersAndAuthorizesAnEmptyPage()
	{
		var ids = Enumerable.Range(1, 25).Select(MemberId).ToArray();
		var rows = new List<Guid> { ids[0] };
		rows.AddRange(ids);
		var context = PageContext(rows);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId) { ProfileIds = ids };
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var first = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		var second = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 20));
		var beyond = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 40));

		Assert.Equal(20, first.Images.Count);
		Assert.Equal(ids[0], first.Images[0].MemberId);
		Assert.Equal(ids[19], first.Images[19].MemberId);
		Assert.Equal(1, first.Images.Count(image => image.MemberId == ids[0]));
		Assert.Equal(ids.Skip(20).ToArray(), second.Images.Select(image => image.MemberId).ToArray());
		Assert.Empty(beyond.Images);
		Assert.Equal(3, gateway.Reads);
		Assert.Equal(25, reader.Members.Count);
		Assert.Equal(ids, reader.Members.Distinct().ToArray());
		Assert.Equal(2, reader.BatchCalls);
		Assert.Equal(20, reader.BatchRequests[0].Length);
		Assert.Equal(5, reader.BatchRequests[1].Length);
	}

	/// <summary>
	/// A failed batch read retains none of that read. An image retained by an earlier request stays.
	/// Cancellation before the read does not call the reader. SQL error 208 is still the missing-storage result.
	/// </summary>
	/// <returns>The task completes when those failures leave the earlier image in place and return no new image.</returns>
	[Fact]
	public async Task Batch_PropagatesCancellationAndReaderFailure()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new FactoryAvatarReader { FailOn = AvatarTestDataFactory.BlueMemberId };
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(1, reader.BatchCalls);
		Assert.Equal(
			[AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId],
			Assert.Single(reader.BatchRequests));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));

		reader.FailOn = null;
		var loaded = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.RedMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		var retained = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded);
		reader.FailOn = AvatarTestDataFactory.BlueMemberId;
		reader.Members.Clear();
		reader.BatchRequests.Clear();
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));

		gateway.Failure = new OperationCanceledException();
		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));

		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		gateway.Failure = null;
		var reads = reader.Members.Count;
		var calls = reader.BatchCalls;
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", cancelled.Token));
		Assert.Equal(reads, reader.Members.Count);
		Assert.Equal(calls, reader.BatchCalls);

		reader.FailOn = null;
		reader.MissingStorage = true;
		reader.Members.Clear();
		reader.BatchRequests.Clear();
		var missing = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None);
		Assert.IsType<WorkspaceAvatarBatchResult.AvatarStorageMissing>(missing);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));
		Assert.Same(retained, context.FindAvatar(AvatarTestDataFactory.RedMemberId));
	}

	/// <summary>
	/// Every retained image skips the batch loader. The profile procedure still runs.
	/// </summary>
	/// <returns>The task completes when the page is returned in preview order without a loader call.</returns>
	[Fact]
	public async Task Batch_DoesNotReadWhenEveryImageIsRetained()
	{
		var context = PageContext([AvatarTestDataFactory.BlueMemberId, AvatarTestDataFactory.RedMemberId]);
		var red = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.RedMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		var blue = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.BlueMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		context.RetainAvatar(AvatarTestDataFactory.RedMemberId, red);
		context.RetainAvatar(AvatarTestDataFactory.BlueMemberId, blue);
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new FactoryAvatarReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(1, gateway.Reads);
		Assert.Equal(0, reader.BatchCalls);
		Assert.Empty(reader.Members);
		Assert.Equal(
			[AvatarTestDataFactory.BlueMemberId, AvatarTestDataFactory.RedMemberId],
			ready.Images.Select(image => image.MemberId).ToArray());
		Assert.Same(blue!.Value.Bytes, ready.Images[0].Bytes);
		Assert.Same(red!.Value.Bytes, ready.Images[1].Bytes);
	}

	/// <summary>
	/// The one miss read records its connection and graph stages once. Member lines keep their own outcome and byte length.
	/// </summary>
	/// <returns>The task completes when the shared stages appear once and the image loop is still recorded.</returns>
	[Fact]
	public async Task Batch_RecordsTheSharedReadOnce()
	{
		var context = PageContext(
		[
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			AvatarTestDataFactory.BlueMemberId
		]);
		var retained = context.RetainAvatar(
			AvatarTestDataFactory.RedMemberId,
			(AvatarTestDataFactory.RedPng, "image/png"));
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds =
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			]
		};
		var reader = new SharedTimingReader();
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 0, profile));

		Assert.Equal(1, reader.BatchCalls);
		Assert.Equal(
			[AvatarTestDataFactory.MemberWithoutAvatarId, AvatarTestDataFactory.BlueMemberId],
			Assert.Single(reader.BatchRequests));
		Assert.Same(retained!.Bytes, ready.Images[0].Bytes);
		Assert.Null(ready.Images[1].Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.NotNull(profile.ImageLoopMs);
		Assert.True(profile.ImageLoopMs >= 0);
		Assert.Equal(11, profile.ImageConnectionOpenMs);
		Assert.Equal(4, profile.SharedGraphTablesMs);
		Assert.Equal(5, profile.SharedTopologySeedMs);
		Assert.Equal(6, profile.SharedTopologyWalkerMs);
		Assert.Equal(7, profile.SharedGraphExpansionMs);
		Assert.Equal(8, profile.SharedAvatarQueryMs);
		Assert.Equal(new[] { "hit", "miss", "miss" }, profile.Members.Select(member => member.Retention).ToArray());
		Assert.Equal(new string?[] { "bytes", "null", "bytes" }, profile.Members.Select(member => member.Image).ToArray());
		Assert.All(profile.Members, member => Assert.Null(member.ConnectionOpenMs));
		Assert.All(profile.Members, member => Assert.Null(member.GraphTablesMs));
		Assert.All(profile.Members, member => Assert.Null(member.AvatarQueryMs));
		Assert.Null(profile.Members[0].ByteLength);
		Assert.Null(profile.Members[1].ByteLength);
		Assert.Equal(AvatarTestDataFactory.BluePng.Length, profile.Members[2].ByteLength);

		profile.SetOutcome("completed");
		var header = profile.HeaderJson();
		Assert.Equal(1, CountOf(header, "\"graphTables\":4"));
		Assert.Equal(1, CountOf(header, "\"imageConnectionOpen\":11"));
		Assert.Equal(3, CountOf(header, "\"graphTables\":null"));
		Assert.Contains("\"byteLength\":" + AvatarTestDataFactory.BluePng.Length.ToString(), header, StringComparison.Ordinal);
	}

	/// <summary>
	/// A reader that still implements only the single load is called once per miss through the batch boundary.
	/// </summary>
	/// <returns>The task completes when those two misses are the only reads.</returns>
	[Fact]
	public async Task DefaultReader_LoopsOnlyTheEligibleMisses()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		context.RetainAvatar(AvatarTestDataFactory.RedMemberId, (AvatarTestDataFactory.RedPng, "image/png"));
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId)
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]
		};
		var reader = new SingleReadReader();
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal([AvatarTestDataFactory.BlueMemberId], reader.Members);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[1].Bytes);
		Assert.Same(AvatarTestDataFactory.RedPng, ready.Images[0].Bytes);
	}

	/// <summary>
	/// The batch command text restricts the image query to the requested ids and the existing member graph check.
	/// This reads the production source. It does not open a connection or execute the command.
	/// </summary>
	[Fact]
	public void BatchSql_IsInspectedAndNotExecuted()
	{
		var source = File.ReadAllText(ControllerPath("AnchorMemberProfileSave.cs"));
		var batch = MethodBody(source, "private static async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadAvatarsAsync(");
		var single = MethodBody(source, "internal static async Task<(byte[] Bytes, string ContentType)?> ReadAvatarAsync(");

		Assert.Contains("RequestedMemberIdTableSql", batch, StringComparison.Ordinal);
		Assert.Contains("new SqlCommand(BatchAvatarQuery, connection)", batch, StringComparison.Ordinal);
		Assert.Contains(Flatten(AnchorMemberProfileSave.RequestedMemberIdTableSql), Flatten(source), StringComparison.Ordinal);
		Assert.Contains(Flatten(AnchorMemberProfileSave.AvatarBatchGraphTableSql), Flatten(source), StringComparison.Ordinal);
		Assert.Contains(Flatten(AnchorMemberProfileSave.DropAvatarBatchCallerTablesSql), Flatten(source), StringComparison.Ordinal);
		Assert.Contains(Flatten(AnchorMemberProfileSave.BatchAvatarQuery), Flatten(source), StringComparison.Ordinal);
		Assert.Contains("#RequestedMemberIds", AnchorMemberProfileSave.BatchAvatarQuery, StringComparison.Ordinal);
		Assert.Contains("requested.MemberId = a.MemberId", AnchorMemberProfileSave.BatchAvatarQuery, StringComparison.Ordinal);
		Assert.Contains("#AvatarBatchGraphEdges", AnchorMemberProfileSave.BatchAvatarQuery, StringComparison.Ordinal);
		Assert.Contains("copied.ChildObjectType = N'member'", AnchorMemberProfileSave.BatchAvatarQuery, StringComparison.Ordinal);
		Assert.Contains("copied.ChildObjectId = a.MemberId", AnchorMemberProfileSave.BatchAvatarQuery, StringComparison.Ordinal);
		Assert.Contains("DROP TABLE IF EXISTS #AvatarBatchGraphEdges", AnchorMemberProfileSave.AvatarBatchGraphTableSql, StringComparison.Ordinal);
		Assert.Contains("CancellationToken.None", source, StringComparison.Ordinal);
		Assert.Equal(0, CountOf(batch, "EXEC dbo.pb_show_tree_step5_topology_seed_builder"));
		Assert.Equal(0, CountOf(batch, "EXEC dbo.pb_show_tree_step5_topology_walker"));
		Assert.Equal(0, CountOf(batch, "EXEC dbo.pb_show_tree_step5_graph_expansion"));
		Assert.DoesNotContain("ReadAvatarAsync", batch, StringComparison.Ordinal);
		Assert.Contains("@RequestedMemberId", batch, StringComparison.Ordinal);

		Assert.Contains("WHERE a.MemberId = @MemberId", single, StringComparison.Ordinal);
		Assert.Contains("#GraphEdges", single, StringComparison.Ordinal);
		Assert.Contains("ge.ChildObjectId = @MemberId", single, StringComparison.Ordinal);
		Assert.Equal(1, CountOf(single, "EXEC dbo.pb_show_tree_step5_topology_seed_builder"));
		Assert.Equal(1, CountOf(single, "EXEC dbo.pb_show_tree_step5_topology_walker"));
		Assert.Equal(1, CountOf(single, "EXEC dbo.pb_show_tree_step5_graph_expansion"));
		Assert.DoesNotContain("#RequestedMemberIds", single, StringComparison.Ordinal);
		Assert.DoesNotContain("#AvatarBatchGraphEdges", single, StringComparison.Ordinal);
		Assert.DoesNotContain("BatchAvatarQuery", single, StringComparison.Ordinal);
	}

	/// <summary>
	/// The single-member endpoint still uses the one-member loader. This reads the production source and does not execute SQL.
	/// </summary>
	/// <returns>The task completes when the factory still returns that one image.</returns>
	[Fact]
	public async Task SingleMemberAvatar_StillUsesTheOneMemberLoader()
	{
		var controller = File.ReadAllText(ControllerPath("MeController.cs"));
		var action = MethodBody(controller, "public async Task<IActionResult> GetMemberAvatar(");
		Assert.Contains("LoadAvatarAsync(", action, StringComparison.Ordinal);
		Assert.DoesNotContain("LoadAvatarsAsync", action, StringComparison.Ordinal);
		Assert.DoesNotContain("ReadManyAsync", action, StringComparison.Ordinal);

		var reader = File.ReadAllText(WorkspacePath("WorkspaceAvatarBatchService.cs"));
		var singleRead = MethodBody(reader, "public async Task<(byte[] Bytes, string ContentType)?> ReadProfiledAsync(");
		var batchRead = MethodBody(reader, "public Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(");
		Assert.Contains("LoadAvatarAsync(", singleRead, StringComparison.Ordinal);
		Assert.Contains("RecordRead(", singleRead, StringComparison.Ordinal);
		Assert.DoesNotContain("LoadAvatarsAsync", singleRead, StringComparison.Ordinal);
		Assert.DoesNotContain("LoadAvatarsAsync(", batchRead, StringComparison.Ordinal);
		Assert.DoesNotContain("RecordSharedRead(", batchRead, StringComparison.Ordinal);
		Assert.Contains("profile procedure copies the graph", batchRead, StringComparison.Ordinal);

		var sessionSource = File.ReadAllText(WorkspacePath("WorkspaceAvatarBatchGraphSession.cs"));
		var profiles = MethodBody(sessionSource, "public async Task<AnchorProfileBundle> ReadProfilesAsync(");
		var images = MethodBody(sessionSource, "public async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadImagesAsync(");
		var dispose = MethodBody(sessionSource, "public async ValueTask DisposeAsync()");
		var createAt = profiles.IndexOf("CreateAvatarBatchGraphTableAsync", StringComparison.Ordinal);
		var procedureAt = profiles.IndexOf("AnchorMemberProfileReader.ExecuteAsync", StringComparison.Ordinal);
		Assert.True(createAt >= 0 && createAt < procedureAt);
		Assert.Contains("SetProfileAuthorization", profiles, StringComparison.Ordinal);
		Assert.Contains("LoadAvatarsAsync(", images, StringComparison.Ordinal);
		Assert.Contains("RecordSharedRead(null, timing)", images, StringComparison.Ordinal);
		Assert.DoesNotContain("CreateGraphTablesAsync", images, StringComparison.Ordinal);
		Assert.DoesNotContain("pb_show_tree_step5", images, StringComparison.Ordinal);
		Assert.Contains("DropAvatarBatchCallerTablesAsync", dispose, StringComparison.Ordinal);
		Assert.DoesNotContain("pb_show_tree_step5", sessionSource, StringComparison.Ordinal);
		Assert.Contains("SetProfileConnectionOpen", sessionSource, StringComparison.Ordinal);

		var detail = File.ReadAllText(WorkspacePath("WorkspaceMemberDetailService.cs"));
		Assert.Contains("ReadProfilesProfiledAsync(", detail, StringComparison.Ordinal);
		Assert.DoesNotContain("AvatarBatchGraph", detail, StringComparison.Ordinal);
		Assert.DoesNotContain("ReadImagesAsync", detail, StringComparison.Ordinal);

		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			AvatarTestDataFactory.AccountId,
			AvatarTestDataFactory.BlueMemberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
		Assert.Equal(AvatarTestDataFactory.BluePng, image!.Value.Bytes);
		Assert.Equal("image/png", image.Value.ContentType);
	}

	/// <summary>
	/// The production batch service authorizes on the shared session, then asks only for retention misses.
	/// A retained image and a member the procedure omits stay out of that image request. The separate reader is not called.
	/// </summary>
	/// <returns>The task completes when the page order, null image, and operation order match.</returns>
	[Fact]
	public async Task SharedConnection_AuthorizesBeforeTheMissQueryAndCleansUp()
	{
		var omitted = Guid.Parse("a1000000-0000-4000-8000-0000000000ee");
		var context = PageContext(
		[
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			omitted,
			AvatarTestDataFactory.BlueMemberId
		]);
		context.RetainAvatar(AvatarTestDataFactory.RedMemberId, (AvatarTestDataFactory.RedPng, "image/png"));
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId);
		var reader = new FactoryAvatarReader();
		var sessions = new RecordingSessions
		{
			ProfileIds =
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			]
		};
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader, sessions);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));

		Assert.Equal(0, gateway.Reads);
		Assert.Equal(0, reader.BatchCalls);
		Assert.Equal(["open", "profiles", "images", "cleanup"], sessions.Operations);
		Assert.Equal(
			[AvatarTestDataFactory.MemberWithoutAvatarId, AvatarTestDataFactory.BlueMemberId],
			Assert.Single(sessions.ImageRequests));
		Assert.Equal(
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			],
			ready.Images.Select(image => image.MemberId).ToArray());
		Assert.Same(AvatarTestDataFactory.RedPng, ready.Images[0].Bytes);
		Assert.Null(ready.Images[1].Bytes);
		Assert.Null(ready.Images[1].ContentType);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.DoesNotContain(ready.Images, image => image.MemberId == omitted);
		Assert.True(sessions.Disposed);

		var retainedOnly = PageContext([AvatarTestDataFactory.BlueMemberId]);
		retainedOnly.RetainAvatar(AvatarTestDataFactory.BlueMemberId, (AvatarTestDataFactory.BluePng, "image/png"));
		sessions.Reset();
		sessions.ProfileIds = [AvatarTestDataFactory.BlueMemberId];
		var retainedService = new WorkspaceAvatarBatchService(Store(retainedOnly), gateway, reader, sessions);
		var retainedReady = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await retainedService.LoadAsync(new ClaimsPrincipal(), retainedOnly.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(["open", "profiles", "cleanup"], sessions.Operations);
		Assert.Empty(sessions.ImageRequests);
		Assert.Same(AvatarTestDataFactory.BluePng, Assert.Single(retainedReady.Images).Bytes);
	}

	/// <summary>
	/// A profile failure, a cancelled profile, and a cancelled image query do not retain a new image.
	/// Each path still drops the caller-owned tables. SQL error 208 from the image query is still the missing-storage result.
	/// </summary>
	/// <returns>The task completes when those failures leave the earlier image in place.</returns>
	[Fact]
	public async Task SharedConnection_FailurePreventsTheImageQueryAndCleansUp()
	{
		var context = PageContext([AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId]);
		var retained = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, (AvatarTestDataFactory.RedPng, "image/png"));
		var gateway = new AuthorizingGateway(AvatarTestDataFactory.AccountId);
		var reader = new FactoryAvatarReader();
		var sessions = new RecordingSessions
		{
			ProfileIds = [AvatarTestDataFactory.RedMemberId, AvatarTestDataFactory.BlueMemberId],
			ProfileFailure = new InvalidOperationException("The profile procedure rejected the anchor.")
		};
		var service = new WorkspaceAvatarBatchService(Store(context), gateway, reader, sessions);

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(["open", "profiles", "cleanup"], sessions.Operations);
		Assert.Empty(sessions.ImageRequests);
		Assert.Equal(0, reader.BatchCalls);
		Assert.Same(retained!.Bytes, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));

		sessions.Reset();
		sessions.ProfileFailure = new OperationCanceledException();
		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(["open", "profiles", "cleanup"], sessions.Operations);
		Assert.Empty(sessions.ImageRequests);

		sessions.Reset();
		sessions.ImageFailure = new OperationCanceledException();
		await Assert.ThrowsAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None));
		Assert.Equal(["open", "profiles", "images", "cleanup"], sessions.Operations);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));
		Assert.Same(retained.Bytes, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);

		sessions.Reset();
		sessions.ImageFailure = CreateSqlException(208);
		var missing = await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None);
		Assert.IsType<WorkspaceAvatarBatchResult.AvatarStorageMissing>(missing);
		Assert.Equal(["open", "profiles", "images", "cleanup"], sessions.Operations);
		Assert.Equal([AvatarTestDataFactory.BlueMemberId], Assert.Single(sessions.ImageRequests));
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.BlueMemberId));
		Assert.Same(retained.Bytes, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);

		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		sessions.Reset();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
			service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", cancelled.Token));
		Assert.Empty(sessions.Operations);
		Assert.Same(retained.Bytes, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);
	}

	/// <summary>
	/// The shared session records profile authorization and the image query. It does not record a second graph build or a second connection.
	/// </summary>
	/// <returns>The task completes when those profile fields match that split.</returns>
	[Fact]
	public async Task SharedConnection_RecordsAuthorizationAndTheImageQueryOnce()
	{
		var context = PageContext(
		[
			AvatarTestDataFactory.RedMemberId,
			AvatarTestDataFactory.MemberWithoutAvatarId,
			AvatarTestDataFactory.BlueMemberId
		]);
		context.RetainAvatar(AvatarTestDataFactory.RedMemberId, (AvatarTestDataFactory.RedPng, "image/png"));
		var sessions = new RecordingSessions
		{
			ProfileIds =
			[
				AvatarTestDataFactory.RedMemberId,
				AvatarTestDataFactory.MemberWithoutAvatarId,
				AvatarTestDataFactory.BlueMemberId
			],
			RecordTimings = true
		};
		var profile = WorkspaceAvatarBatchProfile.ForInitialPage(Guid.NewGuid().ToString("D"), 0);
		Assert.NotNull(profile);
		var service = new WorkspaceAvatarBatchService(
			Store(context),
			new AuthorizingGateway(AvatarTestDataFactory.AccountId),
			new FactoryAvatarReader(),
			sessions);

		var ready = Assert.IsType<WorkspaceAvatarBatchResult.Ready>(
			await service.LoadAsync(new ClaimsPrincipal(), context.ContextId, "system-a", CancellationToken.None, 0, profile));

		Assert.Null(ready.Images[1].Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, ready.Images[2].Bytes);
		Assert.Equal(2, profile.ProfileConnectionOpenMs);
		Assert.Equal(9, profile.ProfileAuthorizationMs);
		Assert.Null(profile.ImageConnectionOpenMs);
		Assert.Null(profile.SharedGraphTablesMs);
		Assert.Null(profile.SharedTopologySeedMs);
		Assert.Null(profile.SharedTopologyWalkerMs);
		Assert.Null(profile.SharedGraphExpansionMs);
		Assert.Equal(8, profile.SharedAvatarQueryMs);
		Assert.All(profile.Members, member => Assert.Null(member.GraphTablesMs));
		Assert.All(profile.Members, member => Assert.Null(member.AvatarQueryMs));
		Assert.Equal(1, CountOf(profile.HeaderJson(), "\"avatarQuery\":8"));
		Assert.Equal(0, CountOf(profile.HeaderJson(), "\"graphTables\":4"));
		Assert.Equal(["open", "profiles", "images", "cleanup"], sessions.Operations);
	}

	/// <summary>Publishes one anchor whose direct members are the supplied ids, including duplicates in that order.</summary>
	/// <param name="memberIds">Direct member ids in preview order.</param>
	/// <returns>The published context.</returns>
	private static WorkspaceContext PageContext(IReadOnlyList<Guid> memberIds)
	{
		var systemId = Guid.Parse("11111111-1111-4111-8111-111111111111");
		var rows = new List<PluralMapPreviewRow>
		{
			new("preview", "system-a", "system-a", "Harbor", "system", systemId, null, false, "Harbor")
		};
		for (var index = 0; index < memberIds.Count; index++)
		{
			rows.Add(new PluralMapPreviewRow(
				"preview",
				"member-" + index.ToString(),
				"member-" + index.ToString(),
				"Member",
				"member",
				memberIds[index],
				"system-a",
				false,
				null));
		}

		var context = WorkspaceContext.Create(
			AvatarTestDataFactory.AccountId,
			[],
			[new WorkspaceOccurrence("account", AvatarTestDataFactory.AccountId, "system", systemId, 0, "system-a")],
			[],
			[],
			rows,
			[new WorkspaceAccessFact(Guid.NewGuid(), "system", systemId, 1, "Active", true)]);
		Store(context);
		return context;
	}

	/// <summary>Stores a context where a later lookup can acquire it. Each call uses a new store.</summary>
	/// <param name="context">The context to publish.</param>
	/// <returns>The store that holds it.</returns>
	private static WorkspaceContextStore Store(WorkspaceContext context)
	{
		var store = new WorkspaceContextStore(
			new ManualWorkspaceClock(),
			new MutableWorkspaceContextSettings(),
			new SilentLogger());
		store.Publish(context);
		return store;
	}

	/// <summary>A numbered member id used to fill a page that has no factory image.</summary>
	/// <param name="index">The member number, 1 through 255.</param>
	/// <returns>The member id.</returns>
	private static Guid MemberId(int index)
	{
		return Guid.Parse("00000000-0000-4000-8000-0000000000" + index.ToString("X2"));
	}

	/// <summary>Builds the SQL exception number the avatar reader reports when its table is absent.</summary>
	/// <param name="number">The SQL error number.</param>
	/// <returns>An exception whose <see cref="SqlException.Number"/> is that value.</returns>
	private static SqlException CreateSqlException(int number)
	{
		var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		var errorCtor = typeof(SqlError).GetConstructors(flags).OrderByDescending(ctor => ctor.GetParameters().Length).First();
		var errorArgs = errorCtor.GetParameters().Select((parameter, index) => Argument(parameter.ParameterType, index == 0 ? number : 0)).ToArray();
		var error = errorCtor.Invoke(errorArgs);
		var collection = Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
		typeof(SqlErrorCollection).GetMethods(flags).First(method => method.Name == "Add").Invoke(collection, [error]);
		var create = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.Where(method => method.Name == "CreateException")
			.Where(method => method.GetParameters().All(parameter =>
				parameter.ParameterType == typeof(SqlErrorCollection)
				|| parameter.ParameterType == typeof(string)
				|| parameter.ParameterType == typeof(Guid)
				|| typeof(Exception).IsAssignableFrom(parameter.ParameterType)))
			.OrderBy(method => method.GetParameters().Length)
			.First();
		var createArgs = create.GetParameters().Select(parameter => Argument(parameter.ParameterType, 0)).ToArray();
		for (var index = 0; index < createArgs.Length; index++)
		{
			if (create.GetParameters()[index].ParameterType == typeof(SqlErrorCollection))
			{
				createArgs[index] = collection;
			}
		}

		return (SqlException)create.Invoke(null, createArgs)!;
	}

	/// <summary>Supplies a constructor argument for a SQL exception type.</summary>
	/// <param name="type">The parameter type.</param>
	/// <param name="number">The integer to use for the first integer parameter.</param>
	/// <returns>A value of that type.</returns>
	private static object? Argument(Type type, int number)
	{
		if (type == typeof(int))
		{
			return number;
		}

		if (type == typeof(byte))
		{
			return (byte)0;
		}

		if (type == typeof(uint))
		{
			return 0u;
		}

		if (type == typeof(Guid))
		{
			return Guid.Empty;
		}

		if (type == typeof(string))
		{
			return "avatar";
		}

		return type.IsValueType ? Activator.CreateInstance(type) : null;
	}

	/// <summary>Resolves one account and returns only the profile ids the test authorizes.</summary>
	private sealed class AuthorizingGateway(Guid accountId) : IWorkspaceProfileGateway
	{
		/// <summary>The account Acquire must match. A different id makes the context unavailable.</summary>
		public Guid AccountId { get; set; } = accountId;

		/// <summary>How many profile reads ran.</summary>
		public int Reads { get; private set; }

		/// <summary>Members the procedure returns. A page member outside this set is omitted.</summary>
		public IReadOnlyList<Guid> ProfileIds { get; set; } = [];

		/// <summary>A failure raised instead of a profile bundle. Cancellation uses this path too.</summary>
		public Exception? Failure { get; set; }

		/// <inheritdoc />
		public Task<AccessContextHelper.Account?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult<AccessContextHelper.Account?>(new AccessContextHelper.Account(
				AccountId,
				"owner@example.com",
				"Owner",
				1,
				new AccessContextHelper.AccountStatus(1, "Active", "Active", 1, true),
				DateTime.UtcNow,
				null));
		}

		/// <inheritdoc />
		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (Failure is not null)
			{
				return Task.FromException<AnchorProfileBundle>(Failure);
			}

			Reads++;
			var profiles = ProfileIds
				.Select(id => new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false))
				.ToArray();
			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], [], [], []));
		}
	}

	/// <summary>
	/// One shared session the production batch service drives. It does not open a database connection.
	/// </summary>
	private sealed class RecordingSessions : IAvatarBatchGraphSessions, IAvatarBatchGraphSession
	{
		/// <summary>open, profiles, images, and cleanup, in the order the service called them.</summary>
		public List<string> Operations { get; } = [];

		/// <summary>Each image request's member ids.</summary>
		public List<Guid[]> ImageRequests { get; } = [];

		/// <summary>Members the procedure returns.</summary>
		public IReadOnlyList<Guid> ProfileIds { get; set; } = [];

		/// <summary>Raised from the profile call. The image query must not run.</summary>
		public Exception? ProfileFailure { get; set; }

		/// <summary>Raised from the image query after the profile call has completed.</summary>
		public Exception? ImageFailure { get; set; }

		/// <summary>True when the image query records the production timing split.</summary>
		public bool RecordTimings { get; set; }

		/// <summary>True after the service disposes the session.</summary>
		public bool Disposed { get; private set; }

		/// <inheritdoc />
		public Task<IAvatarBatchGraphSession> OpenAsync(IWorkspaceGatewayTiming? profile, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Operations.Add("open");
			if (RecordTimings)
			{
				profile?.SetProfileConnectionOpen(2);
			}

			return Task.FromResult<IAvatarBatchGraphSession>(this);
		}

		/// <inheritdoc />
		public Task<AnchorProfileBundle> ReadProfilesAsync(
			string email,
			string objectType,
			string anchorName,
			string branch,
			IWorkspaceGatewayTiming? profile,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Operations.Add("profiles");
			if (ProfileFailure is not null)
			{
				return Task.FromException<AnchorProfileBundle>(ProfileFailure);
			}

			if (RecordTimings)
			{
				profile?.SetProfileAuthorization(9);
			}

			var profiles = ProfileIds
				.Select(id => new AnchorMemberProfile(id, "Member", null, null, null, null, null, null, false))
				.ToArray();
			return Task.FromResult(new AnchorProfileBundle(profiles, true, [], [], [], []));
		}

		/// <inheritdoc />
		public Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadImagesAsync(
			Guid accountId,
			IReadOnlyList<Guid> memberIds,
			WorkspaceAvatarBatchProfile? profile,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Operations.Add("images");
			ImageRequests.Add(memberIds.ToArray());
			if (ImageFailure is not null)
			{
				return Task.FromException<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>>(ImageFailure);
			}

			if (RecordTimings && profile != null)
			{
				var timing = new AnchorMemberProfileSave.AvatarReadTiming();
				timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 8);
				profile.RecordSharedRead(null, timing);
			}

			return AnchorMemberProfileSave.LoadAvatarsAsync(
				useDatabase: false,
				accountId,
				memberIds,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}

		/// <inheritdoc />
		public ValueTask DisposeAsync()
		{
			Operations.Add("cleanup");
			Disposed = true;
			return ValueTask.CompletedTask;
		}

		/// <summary>Clears one run so the next service call starts from an empty operation list.</summary>
		public void Reset()
		{
			Operations.Clear();
			ImageRequests.Clear();
			ProfileFailure = null;
			ImageFailure = null;
			Disposed = false;
		}
	}

	/// <summary>Loads missing images through one factory batch. It records the miss list the service passes.</summary>
	private sealed class FactoryAvatarReader : IWorkspaceAvatarReader
	{
		/// <summary>Members passed to the loader, in request order.</summary>
		public List<Guid> Members { get; } = [];

		/// <summary>How many batch loads ran.</summary>
		public int BatchCalls { get; private set; }

		/// <summary>Each batch load's member ids, in the order the service passed them.</summary>
		public List<Guid[]> BatchRequests { get; } = [];

		/// <summary>A member that makes the batch load fail before any of its images are returned.</summary>
		public Guid? FailOn { get; set; }

		/// <summary>True when the next read reports that avatar storage is not installed.</summary>
		public bool MissingStorage { get; set; }

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The batch path called the single-member reader.");
		}

		/// <inheritdoc />
		public async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(
			Guid accountId,
			IReadOnlyList<Guid> memberIds,
			CancellationToken cancellationToken,
			WorkspaceAvatarBatchProfile? profile = null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			BatchCalls++;
			var requested = memberIds.ToArray();
			BatchRequests.Add(requested);
			Members.AddRange(requested);
			if (MissingStorage)
			{
				throw CreateSqlException(208);
			}

			if (requested.Any(memberId => memberId == FailOn))
			{
				throw new InvalidOperationException("The avatar could not be read.");
			}

			return await AnchorMemberProfileSave.LoadAvatarsAsync(
				useDatabase: false,
				accountId,
				requested,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>Records one shared timing, then loads the miss list through the factory.</summary>
	private sealed class SharedTimingReader : IWorkspaceAvatarReader
	{
		/// <summary>How many batch loads ran.</summary>
		public int BatchCalls { get; private set; }

		/// <summary>Each batch load's member ids.</summary>
		public List<Guid[]> BatchRequests { get; } = [];

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken)
		{
			throw new InvalidOperationException("The batch path called the single-member reader.");
		}

		/// <inheritdoc />
		public async Task<IReadOnlyDictionary<Guid, (byte[] Bytes, string ContentType)>> ReadManyAsync(
			Guid accountId,
			IReadOnlyList<Guid> memberIds,
			CancellationToken cancellationToken,
			WorkspaceAvatarBatchProfile? profile = null)
		{
			BatchCalls++;
			var requested = memberIds.ToArray();
			BatchRequests.Add(requested);
			var timing = new AnchorMemberProfileSave.AvatarReadTiming();
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphTablesStage, 4);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologySeedStage, 5);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.TopologyWalkerStage, 6);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.GraphExpansionStage, 7);
			timing.CompleteStage(AnchorMemberProfileSave.AvatarReadTiming.AvatarQueryStage, 8);
			profile?.RecordSharedRead(11, timing);
			profile?.RecordSharedRead(99, timing);
			return await AnchorMemberProfileSave.LoadAvatarsAsync(
				useDatabase: false,
				accountId,
				requested,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>Implements only the single-member load, so the batch boundary uses its default loop.</summary>
	private sealed class SingleReadReader : IWorkspaceAvatarReader
	{
		/// <summary>Members passed to the single-member load.</summary>
		public List<Guid> Members { get; } = [];

		/// <inheritdoc />
		public Task<(byte[] Bytes, string ContentType)?> ReadAsync(
			Guid accountId,
			Guid memberId,
			CancellationToken cancellationToken)
		{
			Members.Add(memberId);
			return AnchorMemberProfileSave.LoadAvatarAsync(
				useDatabase: false,
				accountId,
				memberId,
				cancellationToken,
				testDataFactory: AvatarTestDataFactory.Read);
		}
	}

	/// <summary>A store logger that keeps no messages.</summary>
	private sealed class SilentLogger : ILogger<WorkspaceContextStore>
	{
		/// <inheritdoc />
		public IDisposable BeginScope<TState>(TState state)
			where TState : notnull
		{
			return SilentScope.Instance;
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel logLevel)
		{
			return false;
		}

		/// <inheritdoc />
		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
		}
	}

	/// <summary>The scope returned by <see cref="SilentLogger"/>.</summary>
	private sealed class SilentScope : IDisposable
	{
		/// <summary>The only scope.</summary>
		public static readonly SilentScope Instance = new();

		/// <inheritdoc />
		public void Dispose()
		{
		}
	}

	/// <summary>Removes whitespace so a command constant can be found in the source file.</summary>
	/// <param name="value">The command text or the file text.</param>
	/// <returns>The same characters without spaces, tabs, or line breaks.</returns>
	private static string Flatten(string value)
	{
		return value.Replace("\r", "", StringComparison.Ordinal)
			.Replace("\n", "", StringComparison.Ordinal)
			.Replace("\t", "", StringComparison.Ordinal)
			.Replace(" ", "", StringComparison.Ordinal);
	}

	/// <summary>Counts a source fragment. This is a text count, not an executed statement.</summary>
	/// <param name="source">The text to search.</param>
	/// <param name="fragment">The fragment to count.</param>
	/// <returns>How many times the fragment occurs.</returns>
	private static int CountOf(string source, string fragment)
	{
		var count = 0;
		var index = 0;
		while ((index = source.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += fragment.Length;
		}

		return count;
	}

	/// <summary>Reads one method through its closing brace. The text is source. It is not compiled or executed here.</summary>
	/// <param name="source">The file text.</param>
	/// <param name="signature">The declaration that starts the method.</param>
	/// <returns>The declaration and its body.</returns>
	private static string MethodBody(string source, string signature)
	{
		var start = source.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, signature);
		var open = source.IndexOf('{', start);
		var depth = 0;
		for (var index = open; index < source.Length; index++)
		{
			if (source[index] == '{')
			{
				depth++;
			}
			else if (source[index] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return source[start..(index + 1)];
				}
			}
		}

		throw new InvalidOperationException("The method body was not closed.");
	}

	/// <summary>Finds a controller source file from this test file.</summary>
	/// <param name="fileName">The file name.</param>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string ControllerPath(string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"Controllers",
			fileName));
	}

	/// <summary>Finds a workspace source file from this test file.</summary>
	/// <param name="fileName">The file name.</param>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string WorkspacePath(string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"Workspace",
			fileName));
	}
}
