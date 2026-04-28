// Dùng chung 2 bên — copy vào cả Server lẫn Unity
namespace Shared.Packets;
public class MovePacket
{
    public float X, Y, Z;

    // object → byte[]
    public byte[] ToBytes()
    {
        using var ms = new System.IO.MemoryStream();
        using var w  = new System.IO.BinaryWriter(ms);

        w.Write(X); w.Write(Y); w.Write(Z);

        byte[] body = ms.ToArray();

        // Gói: [size 4 byte][body]
        byte[] full = new byte[4 + body.Length];
        System.BitConverter.GetBytes(body.Length).CopyTo(full, 0);
        body.CopyTo(full, 4);
        return full;
    }

    // byte[] → object
    public static MovePacket FromBytes(byte[] body)
    {
        using var ms = new System.IO.MemoryStream(body);
        using var r  = new System.IO.BinaryReader(ms);
        return new MovePacket { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle() };
    }
}