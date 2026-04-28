// using Shared.Packets;
// namespace Handlers;
// public static class GameHandler
// {
    // public static async void OnPlayerMove(int sessionId, C_PlayerMovePacket pkt)
    // {
    //     var room = RoomManager.Instance.GetRoomBySession(sessionId);
    //     if (room == null) return;

    //     var session = SessionManager.Instance.Get(sessionId);

    //     // Broadcast cho các người KHÁC trong phòng
    //     var others = room.SessionIds.Where(id => id != sessionId);
    //     await SessionManager.Instance.Broadcast(others, new S_PlayerMovePacket {
    //         PlayerId  = session.PlayerId,
    //         X = pkt.X, Y = pkt.Y, Z = pkt.Z,
    //         Rotation  = pkt.Rotation,
    //         AnimState = pkt.AnimState
    //     });
    // }

    // public static async void OnHitBoss(int sessionId, C_HitBossPacket pkt)
    // {
    //     var room    = RoomManager.Instance.GetRoomBySession(sessionId);
    //     var session = SessionManager.Instance.Get(sessionId);
    //     if (room?.Boss == null || room.Boss.IsDead) return;

    //     // Server tự tính damage thật (chống hack)
    //     int realDamage = CalculateDamage(session.PlayerId, pkt.SkillId);
    //     room.Boss.HpCurrent -= realDamage;

    //     // Broadcast HP mới
    //     await SessionManager.Instance.Broadcast(room.SessionIds, new S_BossHpPacket {
    //         HpCurrent       = room.Boss.HpCurrent,
    //         HpMax           = room.Boss.HpMax,
    //         LastHitPlayerId = session.PlayerId,
    //         Damage          = realDamage
    //     });

    //     if (room.Boss.HpCurrent <= 0)
    //     {
    //         room.Boss.IsDead = true;
    //         await SessionManager.Instance.Broadcast(room.SessionIds, new S_GameResultPacket {
    //             IsVictory = true,
    //             ClearTime = room.GetElapsedSeconds(),
    //             ExpGain   = 500,
    //             GoldGain  = 200
    //         });
    //     }
    // }
//     private static int CalculateDamage(int playerId, int skillId)
//         => new Random().Next(100, 300); // thay bằng logic thật
// }

// public static class SystemHandler
// {
//     public static async void OnPing(int sessionId, C_PingPacket pkt)
//     {
//         await SessionManager.Instance.SendTo(sessionId, new S_PongPacket {
//             Timestamp = pkt.Timestamp
//         });
//     }
// }