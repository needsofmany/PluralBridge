// Copyright (c) 2026 Needs of the Many
// SPDX-License-Identifier: GPL-3.0-or-later

namespace PluralBridge.Api.Tests;

/// <summary>
/// Deterministic avatar bytes for <see cref="Controllers.AnchorMemberProfileSave.LoadAvatarAsync"/> when the database is not selected.
/// </summary>
/// <remarks>
/// Each result is chosen from the supplied account and member ids. The images are one-pixel truecolor PNGs built in this process.
/// This factory does not open a connection, read configuration, or decide whether an account may see a member.
/// </remarks>
internal static class AvatarTestDataFactory
{
	/// <summary>The only account that has fixture rows.</summary>
	internal static readonly Guid AccountId = Guid.Parse("a1000000-0000-4000-8000-000000000001");

	/// <summary>A member whose fixture is a red pixel.</summary>
	internal static readonly Guid RedMemberId = Guid.Parse("a1000000-0000-4000-8000-000000000011");

	/// <summary>A member whose fixture is a blue pixel.</summary>
	internal static readonly Guid BlueMemberId = Guid.Parse("a1000000-0000-4000-8000-000000000012");

	/// <summary>A member of <see cref="AccountId"/> who has no avatar.</summary>
	internal static readonly Guid MemberWithoutAvatarId = Guid.Parse("a1000000-0000-4000-8000-000000000013");

	/// <summary>The red fixture image. Callers leave this array unchanged.</summary>
	internal static readonly byte[] RedPng = SolidPng(255, 0, 0);

	/// <summary>The blue fixture image. Callers leave this array unchanged.</summary>
	internal static readonly byte[] BluePng = SolidPng(0, 0, 255);

	private static readonly Dictionary<(Guid AccountId, Guid MemberId), byte[]?> Fixtures = new()
	{
		[(AccountId, RedMemberId)] = RedPng,
		[(AccountId, BlueMemberId)] = BluePng,
		[(AccountId, MemberWithoutAvatarId)] = null
	};

	/// <summary>
	/// Returns the fixture for one account and member.
	/// </summary>
	/// <param name="accountId">Account id supplied by the loader.</param>
	/// <param name="memberId">Member id supplied by the loader.</param>
	/// <returns>The stored PNG and image/png, or null when that pair has no image. An absent pair and a stored empty avatar are both null.</returns>
	/// <remarks>The same member id under another account is absent, so it does not reuse the known account's image.</remarks>
	internal static (byte[] Bytes, string ContentType)? Read(Guid accountId, Guid memberId)
	{
		if (!Fixtures.TryGetValue((accountId, memberId), out var bytes) || bytes is null)
		{
			return null;
		}

		return (bytes, "image/png");
	}

	/// <summary>
	/// Builds a 1 by 1 truecolor PNG whose only pixel is the supplied color.
	/// </summary>
	/// <param name="red">Red sample, 0 through 255.</param>
	/// <param name="green">Green sample, 0 through 255.</param>
	/// <param name="blue">Blue sample, 0 through 255.</param>
	/// <returns>A PNG whose image data is one uncompressed scanline.</returns>
	/// <remarks>The pixel is stored in a zlib stored block. No image library encodes it.</remarks>
	private static byte[] SolidPng(byte red, byte green, byte blue)
	{
		var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
		var ihdr = Chunk("IHDR",
		[
			0, 0, 0, 1,
			0, 0, 0, 1,
			8, 2, 0, 0, 0
		]);
		var idat = Chunk("IDAT", ZlibStore([0, red, green, blue]));
		var iend = Chunk("IEND", []);
		var png = new byte[signature.Length + ihdr.Length + idat.Length + iend.Length];
		var offset = 0;
		Copy(signature, png, ref offset);
		Copy(ihdr, png, ref offset);
		Copy(idat, png, ref offset);
		Copy(iend, png, ref offset);
		return png;
	}

	/// <summary>Writes one PNG chunk, including its length and CRC.</summary>
	/// <param name="type">Four-character chunk type.</param>
	/// <param name="data">Chunk data. The CRC covers the type and this data.</param>
	/// <returns>The length, type, data, and CRC.</returns>
	private static byte[] Chunk(string type, byte[] data)
	{
		var chunk = new byte[12 + data.Length];
		WriteInt32(chunk, 0, data.Length);
		var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
		typeBytes.CopyTo(chunk, 4);
		data.CopyTo(chunk, 8);
		WriteInt32(chunk, 8 + data.Length, Crc32(chunk.AsSpan(4, 4 + data.Length)));
		return chunk;
	}

	/// <summary>Wraps bytes in a zlib stored block.</summary>
	/// <param name="data">Uncompressed bytes. The avatar fixtures are one four-byte scanline.</param>
	/// <returns>A zlib stream that stores <paramref name="data"/> without compression.</returns>
	private static byte[] ZlibStore(byte[] data)
	{
		var stream = new byte[data.Length + 11];
		stream[0] = 0x78;
		stream[1] = 0x01;
		stream[2] = 0x01;
		stream[3] = (byte)data.Length;
		stream[4] = (byte)(data.Length >> 8);
		var complement = (ushort)~data.Length;
		stream[5] = (byte)complement;
		stream[6] = (byte)(complement >> 8);
		data.CopyTo(stream, 7);
		WriteInt32(stream, 7 + data.Length, Adler32(data));
		return stream;
	}

	/// <summary>Computes the PNG CRC of a chunk type and its data.</summary>
	/// <param name="data">The type bytes followed by the chunk data.</param>
	/// <returns>The CRC value written in the chunk trailer.</returns>
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

	/// <summary>Computes the Adler-32 checksum stored at the end of a zlib stream.</summary>
	/// <param name="data">The uncompressed bytes.</param>
	/// <returns>The checksum, ready to write in network byte order.</returns>
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

	/// <summary>Writes a big-endian 32-bit value.</summary>
	/// <param name="target">Buffer that receives the value.</param>
	/// <param name="offset">First byte to write.</param>
	/// <param name="value">The value.</param>
	private static void WriteInt32(byte[] target, int offset, int value)
	{
		target[offset] = (byte)(value >> 24);
		target[offset + 1] = (byte)(value >> 16);
		target[offset + 2] = (byte)(value >> 8);
		target[offset + 3] = (byte)value;
	}

	/// <summary>Copies one array and advances the destination offset.</summary>
	/// <param name="source">Bytes to copy.</param>
	/// <param name="target">Destination PNG.</param>
	/// <param name="offset">Current write position. It advances by the source length.</param>
	private static void Copy(byte[] source, byte[] target, ref int offset)
	{
		source.CopyTo(target, offset);
		offset += source.Length;
	}
}
