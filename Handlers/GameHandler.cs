using Shared;
using System.Numerics;


public static class BossHandler
{
    // Xử lý khi player tấn công boss
    public static async Task HandleAttackBossAsync(
        GameRoom room,
        int sessionId,
        C_AttackBossPacket packet)
    {
        if (room.bosses.TryGetValue(packet.BossId, out Boss? boss) == false || boss.IsDead)
            return;

        var session = room.GetSession(sessionId);
        if (session == null)
            return;

        // ❌ CHỐNG HACK: Server tự tính damage, không tin client
        int realDamage = CalculateBossDamage(sessionId, packet.SkillId);

        boss.HpCurrent -= realDamage;
        boss.LastDamagePlayerId = sessionId;
        boss.TotalDamageReceived += realDamage;

        // 📤 Broadcast HP Boss update cho tất cả player
        await room.BroadcastOnlyInWorldAsync(new S_BossStatePacket
        {
            BossType = boss.Type,
            BossId = boss.BossId,
            BossX = boss.Position.X,
            BossY = boss.Position.Y,
            HpCurrent = boss.HpCurrent,
            HpMax = boss.HpMax,
            AnimState = boss.AnimState
        });

        // Boss chết
        if (boss.HpCurrent <= 0)
        {
            await DefeatBossAsync(room, packet.BossId);
        }
    }

    // Boss logic mỗi frame (chạy trong FixedUpdate)
    public static void UpdateBossAI(Boss boss, GameRoom room)
    {
        if (boss.IsDead) return;

        // TODO: AI logic - nhắm về player gần nhất, tấn công, v.v
        // Ví dụ: Boss đi về phía player đầu tiên
        var firstPlayer = room.GetFirstPlayer();
        if (firstPlayer != null)
        {
            Vector2 direction = Vector2.Normalize(
                new Vector2(firstPlayer.X, firstPlayer.Y) - boss.Position
            );
            boss.Position += direction * boss.Speed * 0.016f; // 60fps
        }
        _ = room.BroadcastInWorldAsync(new S_BossStatePacket
        {
            BossType = boss.Type,
            BossId = boss.BossId,
            BossX = boss.Position.X,
            BossY = boss.Position.Y,
            HpCurrent = boss.HpCurrent,
            HpMax = boss.HpMax,
            AnimState = boss.AnimState
        });
    }
    private static async Task DefeatBossAsync(GameRoom room, int bossId)
    {
        if (!room.bosses.TryGetValue(bossId, out Boss? boss))
            return;

        boss.IsDead = true;
        var elapsedSeconds = (DateTime.Now - boss.SpawnTime).TotalSeconds;

        // Tính reward dựa trên damage
        // var playerDamages = room.GetPlayerDamageStats(); // Hàm phụ để lấy damage stats

        await room.BroadcastAsync(new S_BossDefeatPacket
        {
            BossId = boss.BossId,
            LastHitPlayerId = boss.LastDamagePlayerId,
            ClearTimeMs = (long)(elapsedSeconds * 1000),
            TotalExpReward = 5000,
            TotalGoldReward = 2000
        });
    }

    private static int CalculateBossDamage(int playerId, int skillId)
    {
        // TODO: Lấy stat từ database hoặc cache
        // ví dụ: player damage (100-300) + skill bonus
        int baseDamage = new Random().Next(100, 300);

        // Skill bonus
        return skillId switch
        {
            1 => (int)(baseDamage * 1.2f), // Skill 1 - 20% bonus
            2 => (int)(baseDamage * 1.5f), // Skill 2 - 50% bonus
            _ => baseDamage
        };
    }
}