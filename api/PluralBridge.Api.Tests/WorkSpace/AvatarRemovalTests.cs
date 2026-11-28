// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PluralBridge.Api.Controllers;
using PluralBridge.Api.Workspace;

namespace PluralBridge.Api.Tests;

/// <summary>
/// Avatar removal is a null pair on the existing profile save, and it drops a retained image.
/// The preparation and retention calls execute production code. They do not open a database connection.
/// The script checks read source. They do not execute the browser files.
/// </summary>
public sealed class AvatarRemovalTests
{
	private static readonly JsonSerializerOptions Json = new()
	{
		PropertyNameCaseInsensitive = true
	};

	/// <summary>Omitted avatar fields leave the stored image unchanged.</summary>
	[Fact]
	public void OmittedAvatar_IsNotARemoval()
	{
		var accepted = AnchorMemberProfileSave.TryPrepareAvatar(
			JsonSerializer.Deserialize<AnchorMemberProfileSaveRequest>("{}", Json),
			out var avatar,
			out var remove,
			out var error);

		Assert.True(accepted);
		Assert.Null(avatar);
		Assert.False(remove);
		Assert.Null(error);
	}

	/// <summary>Both JSON nulls request deletion and do not carry image bytes.</summary>
	[Fact]
	public void NullAvatarFields_RequestRemoval()
	{
		var accepted = AnchorMemberProfileSave.TryPrepareAvatar(
			JsonSerializer.Deserialize<AnchorMemberProfileSaveRequest>(
				"""{"avatarContentType":null,"avatarImageBase64":null}""",
				Json),
			out var avatar,
			out var remove,
			out var error);

		Assert.True(accepted);
		Assert.Null(avatar);
		Assert.True(remove);
		Assert.Null(error);
	}

	/// <summary>One null field is not a removal and is not an image.</summary>
	[Fact]
	public void OneNullAvatarField_IsRejected()
	{
		var accepted = AnchorMemberProfileSave.TryPrepareAvatar(
			JsonSerializer.Deserialize<AnchorMemberProfileSaveRequest>(
				"""{"avatarContentType":null,"avatarImageBase64":"aaaa"}""",
				Json),
			out var avatar,
			out var remove,
			out var error);

		Assert.False(accepted);
		Assert.Null(avatar);
		Assert.False(remove);
		Assert.Equal("An avatar must include its image type and bytes.", error);
	}

	/// <summary>A PNG payload is still a replacement, not a removal.</summary>
	[Fact]
	public void PngPayload_RemainsAReplacement()
	{
		var encoded = Convert.ToBase64String(AvatarTestDataFactory.RedPng);
		var accepted = AnchorMemberProfileSave.TryPrepareAvatar(
			JsonSerializer.Deserialize<AnchorMemberProfileSaveRequest>(
				$$"""{"avatarContentType":"image/png","avatarImageBase64":"{{encoded}}"}""",
				Json),
			out var avatar,
			out var remove,
			out var error);

		Assert.True(accepted);
		Assert.False(remove);
		Assert.Null(error);
		Assert.NotNull(avatar);
		Assert.Equal("image/png", avatar.ContentType);
		Assert.Equal(AvatarTestDataFactory.RedPng, avatar.Bytes);
	}

	/// <summary>
	/// A saved removal drops that member from the account's contexts and leaves every other retained image in place.
	/// </summary>
	/// <returns>The task completes when the released member is absent and the other images remain.</returns>
	[Fact]
	public async Task Release_DropsOnlyTheRemovedMember()
	{
		var accountId = AvatarTestDataFactory.AccountId;
		var otherAccountId = Guid.Parse("a1000000-0000-4000-8000-000000000099");
		var store = new WorkspaceContextStore(
			new ManualWorkspaceClock(),
			new MutableWorkspaceContextSettings(),
			new SilentLogger());
		var mine = OpenContext(accountId);
		var other = OpenContext(otherAccountId);
		store.Publish(mine);
		store.Publish(other);
		mine.RetainAvatar(
			AvatarTestDataFactory.RedMemberId,
			await Load(accountId, AvatarTestDataFactory.RedMemberId));
		mine.RetainAvatar(
			AvatarTestDataFactory.BlueMemberId,
			await Load(accountId, AvatarTestDataFactory.BlueMemberId));
		other.RetainAvatar(
			AvatarTestDataFactory.RedMemberId,
			await Load(accountId, AvatarTestDataFactory.RedMemberId));

		var released = store.ReleaseRetainedAvatar(accountId, AvatarTestDataFactory.RedMemberId);

		Assert.Equal(1, released);
		Assert.Null(mine.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.NotNull(mine.FindAvatar(AvatarTestDataFactory.BlueMemberId));
		Assert.NotNull(other.FindAvatar(AvatarTestDataFactory.RedMemberId));
		Assert.Equal(0, store.ReleaseRetainedAvatar(accountId, AvatarTestDataFactory.RedMemberId));
	}

	/// <summary>
	/// The card, the disabled-write preview, and the save route describe removal without running the browser.
	/// </summary>
	[Fact]
	public void Scripts_DescribeRemovalCancelReplacementAndTheDisabledPreview()
	{
		var card = File.ReadAllText(SourcePath("js", "member-card.js"));
		var members = File.ReadAllText(SourcePath("js", "members.js"));
		var save = File.ReadAllText(ApiSource("AnchorMemberProfileSave.cs"));
		var controller = File.ReadAllText(ApiSource("MeController.cs"));
		var remove = FunctionBody(card, "function removeAvatar()");
		var write = FunctionBody(card, "function writeTracked(control, value)");
		var choose = FunctionBody(card, "avatarFile.addEventListener(\"change\", function ()");
		var request = FunctionBody(card, "function profilePreviewSaveRequest(memberId, request)");
		var apply = FunctionBody(card, "cardDetails.applyWorkspaceAvatar = function (objectUrl)");
		var preview = FunctionBody(members, "async function saveMemberProfilePreview(accountId, memberId, fields)");
		var profileSave = FunctionBody(members, "async function saveMemberProfile(accountId, memberId, anchor, fields)");
		var deleteAvatar = FunctionBody(save, "internal static async Task RemoveAvatarAsync(");

		Assert.Contains("aria-label\", \"Remove avatar\"", card, StringComparison.Ordinal);
		Assert.Contains("avatarRemove.hidden = true", card, StringComparison.Ordinal);
		Assert.Contains("pendingAvatar = { remove: true }", remove, StringComparison.Ordinal);
		Assert.Contains("draftValues.avatarObjectUrl = null", remove, StringComparison.Ordinal);
		Assert.Contains("notifyEdited(avatarWrap)", remove, StringComparison.Ordinal);
		Assert.Contains("draftValues.avatarObjectUrl = value || null", write, StringComparison.Ordinal);
		Assert.Contains("pendingAvatar = {", choose, StringComparison.Ordinal);
		Assert.Contains("base64: normalized.base64", choose, StringComparison.Ordinal);
		Assert.Contains("payload.avatarContentType = null", request, StringComparison.Ordinal);
		Assert.Contains("payload.avatarImageBase64 = null", request, StringComparison.Ordinal);
		Assert.Contains("surfaceIsModified(avatarWrap)", apply, StringComparison.Ordinal);
		Assert.Contains("record.avatarObjectUrl = null", preview, StringComparison.Ordinal);
		Assert.Contains("databaseWritesAreDisabled()", profileSave, StringComparison.Ordinal);
		Assert.Contains("return saveMemberProfilePreview(accountId, memberId, fields);", profileSave, StringComparison.Ordinal);
		Assert.Contains("DELETE FROM dbo.pb_member_avatars", deleteAvatar, StringComparison.Ordinal);
		Assert.Contains("WHERE MemberId = @MemberId", deleteAvatar, StringComparison.Ordinal);
		var committed = controller.IndexOf("await transaction.CommitAsync(cancellationToken);", StringComparison.Ordinal);
		var released = controller.IndexOf("contexts.ReleaseRetainedAvatar(account.AccountId, profile.MemberId);", StringComparison.Ordinal);
		Assert.True(committed >= 0 && released > committed);
	}

	/// <summary>Opens an empty context for one account.</summary>
	/// <param name="accountId">The account that owns the context.</param>
	/// <returns>The context.</returns>
	private static WorkspaceContext OpenContext(Guid accountId)
	{
		return WorkspaceContext.Create(accountId, [], [], [], [], [], []);
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

	/// <summary>Reads one function, starting at its declaration, through the matching closing brace.</summary>
	/// <param name="script">The source file.</param>
	/// <param name="signature">The declaration that starts the function.</param>
	/// <returns>The declaration and its body.</returns>
	private static string FunctionBody(string script, string signature)
	{
		var start = script.IndexOf(signature, StringComparison.Ordinal);
		Assert.True(start >= 0, signature);
		var open = script.IndexOf('{', start);
		var depth = 0;
		for (var index = open; index < script.Length; index++)
		{
			if (script[index] == '{')
			{
				depth++;
			}
			else if (script[index] == '}')
			{
				depth--;
				if (depth == 0)
				{
					return script[start..(index + 1)];
				}
			}
		}

		throw new InvalidOperationException("The function body was not closed.");
	}

	/// <summary>Finds a controller source file from this test file.</summary>
	/// <param name="fileName">The controller file name.</param>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string ApiSource(string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"PluralBridge.Api",
			"PluralBridge.Api",
			"Controllers",
			fileName));
	}

	/// <summary>Finds a browser source file from this test file.</summary>
	/// <param name="folder">The source folder under app/src, or a parent step when the path leaves that tree.</param>
	/// <param name="fileName">The next path part.</param>
	/// <param name="sourceFile">The test file path supplied by the compiler.</param>
	/// <returns>The absolute source path.</returns>
	private static string SourcePath(string folder, string fileName, [CallerFilePath] string sourceFile = "")
	{
		return Path.GetFullPath(Path.Combine(
			Path.GetDirectoryName(sourceFile) ?? string.Empty,
			"..",
			"..",
			"..",
			"app",
			"src",
			folder,
			fileName));
	}

	/// <summary>Discards store accounting logs.</summary>
	private sealed class SilentLogger : ILogger<WorkspaceContextStore>
	{
		/// <inheritdoc />
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		{
			return null;
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
}
