// using Shared.Packets;
// namespace Handlers;
// public static class RoomHandler
// {
//     public static async void OnCreateRoom(int sessionId, C_CreateRoomPacket pkt)
//     {
//         var session = SessionManager.Instance.Get(sessionId);
//         if (session == null) return;

//         var room = RoomManager.Instance.CreateRoom(pkt.RoomName, session.PlayerId, pkt.BossId, pkt.MaxPlayers);

//         await SessionManager.Instance.SendTo(sessionId, new S_CreateRoomPacket {
//             Success = true,
//             RoomId  = room.RoomId
//         });
//     }

//     public static async void OnJoinRoom(int sessionId, C_JoinRoomPacket pkt)
//     {
//         var session = SessionManager.Instance.Get(sessionId);
//         var room    = RoomManager.Instance.GetRoom(pkt.RoomId);

//         if (room == null || room.IsFull)
//         {
//             await SessionManager.Instance.SendTo(sessionId, new S_ErrorPacket {
//                 ErrorCode = 400, Message = room == null ? "Room not found" : "Room is full"
//             });
//             return;
//         }

//         room.AddPlayer(session);

//         // Broadcast cho tất cả trong phòng
//         var readyUpdate = new S_ReadyUpdatePacket {
//             PlayerId = session.PlayerId,
//             IsReady  = false
//         };
//         await SessionManager.Instance.Broadcast(room.SessionIds, readyUpdate);
//     }

//     public static async void OnReady(int sessionId, C_ReadyPacket pkt)
//     {
//         var room = RoomManager.Instance.GetRoomBySession(sessionId);
//         if (room == null) return;

//         room.SetReady(sessionId, pkt.IsReady);

//         await SessionManager.Instance.Broadcast(room.SessionIds, new S_ReadyUpdatePacket {
//             PlayerId = SessionManager.Instance.Get(sessionId).PlayerId,
//             IsReady  = pkt.IsReady
//         });
//     }

//     public static async void OnStartGame(int sessionId, C_StartGamePacket pkt)
//     {
//         var room = RoomManager.Instance.GetRoomBySession(sessionId);
//         if (room == null || !room.AllReady) return;

//         room.Status = "playing";

//         await SessionManager.Instance.Broadcast(room.SessionIds, new S_StartGamePacket {
//             BossId         = room.BossId,
//             Difficulty     = room.Difficulty,
//             StartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
//         });
//     }
// }