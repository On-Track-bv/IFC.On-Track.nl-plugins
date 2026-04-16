// Purpose: Generates deterministic UUID v5 from a URI string (used for Revit shared parameter GUIDs)
using System.Security.Cryptography;
using System.Text;

namespace IfcOnTrack.Revit.Utilities;

/// <summary>
/// Generates a consistent UUID from a URI string.
/// Used to create stable Revit shared parameter GUIDs for bSDD classification and property parameters.
/// Algorithm: UUID v5 (SHA1-based, namespaced).
/// </summary>
public static class UuidFromUri
{
    private static readonly Guid BsddNamespace = new("b989028b-337b-417f-b917-c4e17384b8c5");

    /// <summary>
    /// Create a stable UUID from a URI string (e.g., a bSDD parameter name).
    /// </summary>
    public static Guid CreateUuidFromUri(string input)
    {
        byte[] data = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
        var hashedGuid = new Guid(data.Take(16).ToArray());
        return CreateGuidV5(BsddNamespace, hashedGuid.ToByteArray());
    }

    private static Guid CreateGuidV5(Guid namespaceId, byte[] nameBytes)
    {
        byte[] namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);

        byte[] hash;
        using (var sha1 = SHA1.Create())
        {
            sha1.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
            sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
            hash = sha1.Hash!;
        }

        var newGuid = new byte[16];
        Array.Copy(hash, 0, newGuid, 0, 16);
        newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);
        SwapByteOrder(newGuid);
        return new Guid(newGuid);
    }

    private static void SwapByteOrder(byte[] guid)
    {
        Swap(guid, 0, 3);
        Swap(guid, 1, 2);
        Swap(guid, 4, 5);
        Swap(guid, 6, 7);
    }

    private static void Swap(byte[] guid, int left, int right)
    {
        (guid[left], guid[right]) = (guid[right], guid[left]);
    }
}
