// namespace Shared;
// public static class PacketSerializer
// {
//     // object → byte[]
//     public static byte[] Serialize(BasePacket packet)
//     {
//         using var ms = new MemoryStream();
//         using var writer = new BinaryWriter(ms);

//         writer.Write((short)0);           // placeholder size
//         writer.Write((short)packet.Type); // type
//         packet.Write(writer);             // body

//         byte[] data = ms.ToArray();

//         // Ghi lại size thật vào 2 byte đầu
//         BitConverter.GetBytes((short)data.Length).CopyTo(data, 0);
//         return data;
//     }

//     // byte[] → object
//     public static T Deserialize<T>(byte[] buffer) where T : BasePacket, new()
//     {
//         using var ms = new MemoryStream(buffer);
//         using var reader = new BinaryReader(ms);

//         reader.ReadInt16(); // skip size
//         reader.ReadInt16(); // skip type

//         var packet = new T();
//         packet.Read(reader);
//         return packet;
//     }

//     // Chỉ đọc type, không deserialize toàn bộ
//     public static PacketType PeekType(byte[] buffer)
//         => (PacketType)BitConverter.ToInt16(buffer, 2);
// }