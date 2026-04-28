// using System.Numerics;
// namespace Shared.Packets;
// public class C_PlayerMovePacket : BasePacket
// {
//     public override PacketType Type => PacketType.C_PLAYER_MOVE;
//     public float X, Y, Z, Rotation;
//     public int AnimState;

//     public override void Write(BinaryWriter w)
//     { w.Write(X); w.Write(Y); w.Write(Z); w.Write(Rotation); w.Write(AnimState); }
//     public override void Read(BinaryReader r)
//     { X = r.ReadSingle(); Y = r.ReadSingle(); Z = r.ReadSingle(); Rotation = r.ReadSingle(); AnimState = r.ReadInt32(); }
// }

// public class S_PlayerMovePacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_PLAYER_MOVE;
//     public int PlayerId;
//     public float X, Y, Z, Rotation;
//     public int AnimState;

//     public override void Write(BinaryWriter w)
//     { w.Write(PlayerId); w.Write(X); w.Write(Y); w.Write(Z); w.Write(Rotation); w.Write(AnimState); }
//     public override void Read(BinaryReader r)
//     { PlayerId = r.ReadInt32(); X = r.ReadSingle(); Y = r.ReadSingle(); Z = r.ReadSingle(); Rotation = r.ReadSingle(); AnimState = r.ReadInt32(); }
// }

// public class C_HitBossPacket : BasePacket
// {
//     public override PacketType Type => PacketType.C_HIT_BOSS;
//     public int SkillId;

//     public override void Write(BinaryWriter w) => w.Write(SkillId);
//     public override void Read(BinaryReader r) => SkillId = r.ReadInt32();
// }

// public class S_BossHpPacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_BOSS_HP;
//     public float HpCurrent, HpMax;
//     public int LastHitPlayerId, Damage;

//     public override void Write(BinaryWriter w)
//     { w.Write(HpCurrent); w.Write(HpMax); w.Write(LastHitPlayerId); w.Write(Damage); }
//     public override void Read(BinaryReader r)
//     { HpCurrent = r.ReadSingle(); HpMax = r.ReadSingle(); LastHitPlayerId = r.ReadInt32(); Damage = r.ReadInt32(); }
// }

// public class S_BossMovePacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_BOSS_MOVE;
//     public float X, Y, Z, Rotation;
//     public int AnimState, TargetPlayerId;

//     public override void Write(BinaryWriter w)
//     { w.Write(X); w.Write(Y); w.Write(Z); w.Write(Rotation); w.Write(AnimState); w.Write(TargetPlayerId); }
//     public override void Read(BinaryReader r)
//     { X = r.ReadSingle(); Y = r.ReadSingle(); Z = r.ReadSingle(); Rotation = r.ReadSingle(); AnimState = r.ReadInt32(); TargetPlayerId = r.ReadInt32(); }
// }

// public class S_BossSkillPacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_BOSS_SKILL;
//     public int SkillId, TargetPlayerId;
//     public float TargetX, TargetZ, Delay;

//     public override void Write(BinaryWriter w)
//     { w.Write(SkillId); w.Write(TargetPlayerId); w.Write(TargetX); w.Write(TargetZ); w.Write(Delay); }
//     public override void Read(BinaryReader r)
//     { SkillId = r.ReadInt32(); TargetPlayerId = r.ReadInt32(); TargetX = r.ReadSingle(); TargetZ = r.ReadSingle(); Delay = r.ReadSingle(); }
// }

// public class S_PlayerDamagePacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_PLAYER_DAMAGE;
//     public int PlayerId, Damage, HpCurrent, HpMax;
//     public bool IsDead;

//     public override void Write(BinaryWriter w)
//     { w.Write(PlayerId); w.Write(Damage); w.Write(HpCurrent); w.Write(HpMax); w.Write(IsDead); }
//     public override void Read(BinaryReader r)
//     { PlayerId = r.ReadInt32(); Damage = r.ReadInt32(); HpCurrent = r.ReadInt32(); HpMax = r.ReadInt32(); IsDead = r.ReadBoolean(); }
// }

// public class S_GameResultPacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_GAME_RESULT;
//     public bool IsVictory;
//     public int ClearTime;
//     public int ExpGain, GoldGain;

//     public override void Write(BinaryWriter w)
//     { w.Write(IsVictory); w.Write(ClearTime); w.Write(ExpGain); w.Write(GoldGain); }
//     public override void Read(BinaryReader r)
//     { IsVictory = r.ReadBoolean(); ClearTime = r.ReadInt32(); ExpGain = r.ReadInt32(); GoldGain = r.ReadInt32(); }
// }

// public class C_PingPacket : BasePacket
// {
//     public override PacketType Type => PacketType.C_PING;
//     public long Timestamp;

//     public override void Write(BinaryWriter w) => w.Write(Timestamp);
//     public override void Read(BinaryReader r) => Timestamp = r.ReadInt64();
// }

// public class S_PongPacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_PONG;
//     public long Timestamp;

//     public override void Write(BinaryWriter w) => w.Write(Timestamp);
//     public override void Read(BinaryReader r) => Timestamp = r.ReadInt64();
// }

// public class S_ErrorPacket : BasePacket
// {
//     public override PacketType Type => PacketType.S_ERROR;
//     public int ErrorCode;
//     public string? Message;

//     public override void Write(BinaryWriter w) { w.Write(ErrorCode); w.Write(Message ?? ""); }
//     public override void Read(BinaryReader r) { ErrorCode = r.ReadInt32(); Message = r.ReadString(); }
// }
// public class C_PlayerInputPacket : BasePacket
// {
//     public override PacketType Type => PacketType.C_PLAYER_INPUT;
//     public Vector2 inputDirection;

//     public override void Write(BinaryWriter w) { w.Write(inputDirection.X); w.Write(inputDirection.Y); }
//     public override void Read(BinaryReader r)  { inputDirection = new Vector2(r.ReadSingle(), r.ReadSingle()); }
// }