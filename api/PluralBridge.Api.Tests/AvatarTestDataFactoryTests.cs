// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

using PluralBridge.Api.Controllers;

namespace PluralBridge.Api.Tests;

/// <summary>
/// The concrete avatar factory returns its fixture through the common loader.
/// These calls check byte transport. They do not open a database connection or establish graph authorization.
/// </summary>
public sealed class AvatarTestDataFactoryTests
{
	/// <summary>
	/// Each known member keeps its own PNG. A second load returns the same bytes.
	/// </summary>
	/// <returns>The task completes when both images match their fixtures and differ from each other.</returns>
	[Fact]
	public async Task KnownMembers_ReturnTheirOwnPngBytes()
	{
		var red = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.RedMemberId);
		var redAgain = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.RedMemberId);
		var blue = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.BlueMemberId);

		Assert.NotNull(red);
		Assert.NotNull(redAgain);
		Assert.NotNull(blue);
		Assert.Equal("image/png", red.Value.ContentType);
		Assert.Equal("image/png", blue.Value.ContentType);
		Assert.Equal(AvatarTestDataFactory.RedPng, red.Value.Bytes);
		Assert.Equal(AvatarTestDataFactory.RedPng, redAgain.Value.Bytes);
		Assert.Equal(AvatarTestDataFactory.BluePng, blue.Value.Bytes);
		Assert.NotEqual(AvatarTestDataFactory.RedPng, AvatarTestDataFactory.BluePng);
		AssertSolidPixel(red.Value.Bytes, 255, 0, 0);
		AssertSolidPixel(blue.Value.Bytes, 0, 0, 255);
	}

	/// <summary>
	/// A member with no avatar, an unknown pair, and a known member under another account are all null.
	/// </summary>
	/// <returns>The task completes when each of those pairs returns null.</returns>
	[Fact]
	public async Task MissingAndUnknownPairs_ReturnNull()
	{
		var missing = await Load(AvatarTestDataFactory.AccountId, AvatarTestDataFactory.MemberWithoutAvatarId);
		var unknown = await Load(
			Guid.Parse("a1000000-0000-4000-8000-0000000000aa"),
			Guid.Parse("a1000000-0000-4000-8000-0000000000bb"));
		var otherAccount = await Load(
			Guid.Parse("a1000000-0000-4000-8000-000000000099"),
			AvatarTestDataFactory.RedMemberId);

		Assert.Null(missing);
		Assert.Null(unknown);
		Assert.Null(otherAccount);
	}

	/// <summary>
	/// Loads one fixture through the common method and requires the database stages to stay unused.
	/// </summary>
	/// <param name="accountId">Account id passed to the factory.</param>
	/// <param name="memberId">Member id passed to the factory.</param>
	/// <returns>The factory result. Null when that pair has no image.</returns>
	/// <remarks>The connection argument is null. A database read would have to use that connection and would record a stage.</remarks>
	private static async Task<(byte[] Bytes, string ContentType)?> Load(Guid accountId, Guid memberId)
	{
		var timing = new AnchorMemberProfileSave.AvatarReadTiming();
		var image = await AnchorMemberProfileSave.LoadAvatarAsync(
			useDatabase: false,
			accountId,
			memberId,
			CancellationToken.None,
			connection: null,
			timing: timing,
			testDataFactory: AvatarTestDataFactory.Read);

		Assert.Null(timing.GraphTablesMilliseconds);
		Assert.Null(timing.TopologySeedMilliseconds);
		Assert.Null(timing.TopologyWalkerMilliseconds);
		Assert.Null(timing.GraphExpansionMilliseconds);
		Assert.Null(timing.AvatarQueryMilliseconds);
		Assert.Null(timing.FailedStage);
		Assert.Null(timing.ByteLength);
		return image;
	}

	/// <summary>
	/// Reads a 1 by 1 truecolor PNG and checks its only pixel.
	/// </summary>
	/// <param name="png">PNG bytes returned by the loader.</param>
	/// <param name="red">Expected red sample.</param>
	/// <param name="green">Expected green sample.</param>
	/// <param name="blue">Expected blue sample.</param>
	private static void AssertSolidPixel(byte[] png, byte red, byte green, byte blue)
	{
		Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
		var offset = 8;
		byte[]? scanline = null;
		while (offset + 12 <= png.Length)
		{
			var length = ReadInt32(png, offset);
			var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
			var data = png.AsSpan(offset + 8, length);
			Assert.Equal(Crc32(png.AsSpan(offset + 4, 4 + length)), ReadInt32(png, offset + 8 + length));
			if (type == "IHDR")
			{
				Assert.Equal(13, length);
				Assert.Equal(1, ReadInt32(data.ToArray(), 0));
				Assert.Equal(1, ReadInt32(data.ToArray(), 4));
				Assert.Equal(8, data[8]);
				Assert.Equal(2, data[9]);
			}
			else if (type == "IDAT")
			{
				scanline = ReadStoredZlib(data.ToArray());
			}

			offset += 12 + length;
			if (type == "IEND")
			{
				break;
			}
		}

		Assert.NotNull(scanline);
		Assert.Equal(new byte[] { 0, red, green, blue }, scanline);
	}

	/// <summary>Reads the one stored block inside a fixture zlib stream.</summary>
	/// <param name="zlib">IDAT payload.</param>
	/// <returns>The uncompressed scanline.</returns>
	private static byte[] ReadStoredZlib(byte[] zlib)
	{
		Assert.Equal(0x78, zlib[0]);
		Assert.Equal(0x01, zlib[1]);
		Assert.Equal(0x01, zlib[2]);
		var length = zlib[3] | (zlib[4] << 8);
		var stored = zlib.AsSpan(7, length).ToArray();
		Assert.Equal(Adler32(stored), ReadInt32(zlib, 7 + length));
		return stored;
	}

	/// <summary>Computes a PNG chunk CRC.</summary>
	/// <param name="data">Chunk type and data.</param>
	/// <returns>The expected trailer value.</returns>
	private static int Crc32(ReadOnlySpan<byte> data)
	{
		var crc = 0xFFFFFFFFu;
		foreach (var value in data)
		{
			crc ^= value;
			for (var bit = 0; bit < 8; bit++)
			{
				var mask = (uint)-(crc & 1);
				crc = (crc >> 1) ^ (0xEDB88320u & mask);
			}
		}

		return (int)~crc;
	}

	/// <summary>Computes the Adler-32 value of an uncompressed scanline.</summary>
	/// <param name="data">Uncompressed bytes.</param>
	/// <returns>The checksum in network byte order.</returns>
	private static int Adler32(byte[] data)
	{
		var a = 1;
		var b = 0;
		foreach (var value in data)
		{
			a = (a + value) % 65521;
			b = (b + a) % 65521;
		}

		return (b << 16) | a;
	}

	/// <summary>Reads a big-endian 32-bit value.</summary>
	/// <param name="data">Source bytes.</param>
	/// <param name="offset">First byte.</param>
	/// <returns>The integer value.</returns>
	private static int ReadInt32(byte[] data, int offset)
	{
		return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
	}
}
