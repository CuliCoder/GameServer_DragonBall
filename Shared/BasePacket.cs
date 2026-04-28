// namespace Shared;
// public abstract class BasePacket
// {
//     public abstract PacketType Type { get; }
//     public abstract void Write(BinaryWriter writer); // ghi body
//     public abstract void Read(BinaryReader reader);  // đọc body
// }

// public enum PacketType : short
// {
//     C_LOGIN         = 1,
//     S_LOGIN         = 2,
//     C_CREATE_ROOM   = 10,
//     S_CREATE_ROOM   = 11,
//     C_JOIN_ROOM     = 12,
//     S_JOIN_ROOM     = 13,
//     S_ROOM_UPDATE   = 18,
//     C_READY         = 19,
//     S_READY_UPDATE  = 20,
//     C_START_GAME    = 21,
//     S_START_GAME    = 22,
//     C_PLAYER_MOVE   = 30,
//     S_PLAYER_MOVE   = 31,
//     C_USE_SKILL     = 32,
//     S_USE_SKILL     = 33,
//     C_HIT_BOSS      = 34,
//     S_BOSS_HP       = 35,
//     S_BOSS_MOVE     = 36,
//     S_BOSS_SKILL    = 37,
//     S_PLAYER_DAMAGE = 38,
//     S_BOSS_DEAD     = 40,
//     S_GAME_RESULT   = 50,
//     C_PING          = 90,
//     S_PONG          = 91,
//     S_ERROR         = 99,
//     C_PLAYER_INPUT   = 100,
//     C_LEAVE_ROOM       = 101,
// }