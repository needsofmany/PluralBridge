// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The avatar loader chooses the supplied factory when the database is not selected.
/// These calls use that method. They do not start the application or open a database connection.
/// </summary>
public sealed class AvatarLoadSelectionTests
{
	/// <summary>
	/// The factory path returns the supplied image and passes the account and member ids through.
	/// </summary>
	/// <returns>The task completes when the returned image is the factory image.</returns>
	[Fact]
	public async Task FactorySelection_ReturnsTheSuppliedImageForThoseIds()
	{
		var accountId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
		var memberId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
		var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
		Guid? seenAccount = null;
		Guid? seenMember = null;

		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			accountId,
			memberId,
			CancellationToken.None,
			testDataFactory: (account, member) =>
			{
				seenAccount = account;
				seenMember = member;
				return (bytes, "image/png");
			});

		Assert.NotNull(image);
		Assert.Same(bytes, image.Value.Bytes);
		Assert.Equal("image/png", image.Value.ContentType);
		Assert.Equal(accountId, seenAccount);
		Assert.Equal(memberId, seenMember);
	}

	/// <summary>
	/// A factory that has no image stays null. The ids are still passed to that factory.
	/// </summary>
	/// <returns>The task completes when the loader returns null.</returns>
	[Fact]
	public async Task FactorySelection_KeepsANullImage()
	{
		var accountId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
		var memberId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
		Guid? seenAccount = null;
		Guid? seenMember = null;

		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			accountId,
			memberId,
			CancellationToken.None,
			testDataFactory: (account, member) =>
			{
				seenAccount = account;
				seenMember = member;
				return null;
			});

		Assert.Null(image);
		Assert.Equal(accountId, seenAccount);
		Assert.Equal(memberId, seenMember);
	}

	/// <summary>
	/// The factory path completes without a connection and without starting the database read's stages.
	/// </summary>
	/// <returns>The task completes when the factory image is returned and the timing object is untouched.</returns>
	/// <remarks>The connection argument is null. Entering the database reader would have to use that connection.</remarks>
	[Fact]
	public async Task FactorySelection_DoesNotOpenAConnection()
	{
		var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		var called = false;

		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee"),
			Guid.Parse("ffffffff-ffff-4fff-8fff-ffffffffffff"),
			CancellationToken.None,
			connection: null,
			timing: timing,
			testDataFactory: (_, _) =>
			{
				called = true;
				return (bytes, "image/jpeg");
			});

		Assert.True(called);
		Assert.NotNull(image);
		Assert.Same(bytes, image.Value.Bytes);
		Assert.Equal("image/jpeg", image.Value.ContentType);
		Assert.Null(timing.GraphTablesMilliseconds);
		Assert.Null(timing.TopologySeedMilliseconds);
		Assert.Null(timing.TopologyWalkerMilliseconds);
		Assert.Null(timing.GraphExpansionMilliseconds);
		Assert.Null(timing.AvatarQueryMilliseconds);
		Assert.Null(timing.FailedStage);
		Assert.Null(timing.ByteLength);
	}
}
