// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// A workspace context retains loaded avatars for its own members.
/// These calls use the production retention methods and the common loader. They do not open a database connection.
/// </summary>
public sealed class WorkspaceAvatarRetentionTests
{
	/// <summary>
	/// A loaded image stays on its context, and both occurrences of that member use the same entry.
	/// </summary>
	/// <returns>The task completes when the retained bytes and content type match the fixture.</returns>
	[Fact]
	public async Task Context_RetainsOneImageForRepeatedOccurrences()
	{
		var context = OpenContext();
		var loaded = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.RedMemberId);

		var first = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded);
		var second = context.RetainAvatar(AvatarTestDataFactory.RedMemberId, loaded);
		var found = context.FindAvatar(AvatarTestDataFactory.RedMemberId);

		Assert.NotNull(loaded);
		Assert.NotNull(first);
		Assert.NotNull(found);
		Assert.Same(first, second);
		Assert.Same(first, found);
		Assert.Same(loaded.Value.Bytes, found.Bytes);
		Assert.Equal(AvatarTestDataFactory.RedPng, found.Bytes);
		Assert.Equal("image/png", found.ContentType);
	}

	/// <summary>
	/// Each member keeps the image selected for that member.
	/// </summary>
	/// <returns>The task completes when the two retained images differ.</returns>
	[Fact]
	public async Task Context_RetainsADifferentImagePerMember()
	{
		var context = OpenContext();
		var red = context.RetainAvatar(
			AvatarTestDataFactory.RedMemberId,
			await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.RedMemberId));
		var blue = context.RetainAvatar(
			AvatarTestDataFactory.BlueMemberId,
			await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.BlueMemberId));

		Assert.NotNull(red);
		Assert.NotNull(blue);
		Assert.NotSame(red, blue);
		Assert.Equal(AvatarTestDataFactory.RedPng, context.FindAvatar(AvatarTestDataFactory.RedMemberId)!.Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, context.FindAvatar(AvatarTestDataFactory.BlueMemberId)!.Bytes);
		Assert.Equal("image/png", red.ContentType);
		Assert.Equal("image/png", blue.ContentType);
	}

	/// <summary>
	/// Another context cannot see an image retained by the first context.
	/// </summary>
	/// <returns>The task completes when the second context has no entry for that member.</returns>
	[Fact]
	public async Task AnotherContext_CannotRetrieveTheRetainedImage()
	{
		var first = OpenContext();
		var second = OpenContext();
		first.RetainAvatar(
			AvatarTestDataFactory.RedMemberId,
			await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.RedMemberId));

		Assert.Null(second.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.NotNull(first.FindAvatar(AvatarTestDataFactory.RedMemberId));
	}

	/// <summary>
	/// A member with no avatar does not gain an image entry.
	/// </summary>
	/// <returns>The task completes when retention and lookup both return null.</returns>
	[Fact]
	public async Task MissingAvatar_DoesNotCreateAnEntry()
	{
		var context = OpenContext();
		var loaded = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.MemberWithoutAvatarId);

		var retained = context.RetainAvatar(AvatarTestDataFactory.MemberWithoutAvatarId, loaded);

		Assert.Null(loaded);
		Assert.Null(retained);
		Assert.Null(context.FindAvatar(AvatarTestDataFactory.MemberWithoutAvatarId));
	}

	/// <summary>Opens an empty context. The avatar collection starts empty on that instance.</summary>
	/// <returns>The context.</returns>
	private static WorkspaceContext OpenContext()
	{
		return WorkspaceContext.Create(
			AvatarTestDataFactory.AccountId,
			[],
			[],
			[],
			[],
			[],
			[]);
	}

	/// <summary>Loads one fixture through the common method without a database connection.</summary>
	/// <param name="accountId">Account id passed to the factory.</param>
	/// <param name="memberId">Member id passed to the factory.</param>
	/// <returns>The factory image, or null when that pair has none.</returns>
	private static Task<(byte[] Bytes, string ContentType)?> Load(Guid accountId, Guid memberId)
	{
		return AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			accountId,
			memberId,
			CancellationToken.None,
			testDataFactory: AvatarTestDataFactory.Read);
	}
}
