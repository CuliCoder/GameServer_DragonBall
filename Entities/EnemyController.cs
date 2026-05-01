using Shared;
using System.Numerics;

public enum EnemyActionState
{
    Idle,
    Move,
    Punch,
    Strike,
    Die
}
public class EnemyController
{

    private float _actionTimer;
    private float _actionInterval = 2.0f; // Thay đổi hành động mỗi 2 giây
    private EnemyActionState _currentState = EnemyActionState.Idle;
    private PlayerState _targetPlayer = null!;
    public int BossId { get; set; }
    public BossType Type { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }

    public int HpMax { get; set; }
    public int HpCurrent { get; set; }
    public bool IsDead { get; set; }

    public int Level { get; set; }
    public float Speed { get; set; }

    public int LastDamagePlayerId { get; set; }
    public DateTime SpawnTime { get; set; }
    public int TotalDamageReceived { get; set; }
    private bool DoneMoveToPlayer = true;
    public EnemyController(int bossId, BossType type, Vector2 position, int hpMax, int level, float speed)
    {
        BossId = bossId;
        Type = type;
        Position = position;
        HpMax = hpMax;
        HpCurrent = hpMax;
        Level = level;
        Speed = speed;
        SpawnTime = DateTime.Now;
    }
    // Xử lý khi player tấn công boss
    public async Task HandleAttackBossAsync(GameRoom room, C_AttackBossPacket packet, int sessionId)
    {

        PlayerState? playerState = room.GetPlayerState(sessionId);
        if (playerState == null || IsDead)
            return;
        // ❌ CHỐNG HACK: Server tự tính damage, không tin client
        int realDamage = CalculateBossDamage(packet.SkillId, playerState ?? null!);

        HpCurrent -= realDamage;
        LastDamagePlayerId = sessionId;
        TotalDamageReceived += realDamage;

        // 📤 Broadcast HP Boss update cho tất cả player
        await room.BroadcastOnlyInWorldAsync(new S_BossStatePacket
        {
            BossType = Type,
            BossId = BossId,
            BossX = Position.X,
            BossY = Position.Y,
            HpCurrent = HpCurrent,
            HpMax = HpMax,
            AnimState = _currentState,
            speed = Speed
        });

        // Boss chết
        if (HpCurrent <= 0)
        {
            await DefeatBossAsync(room);
        }
    }

    // Boss logic mỗi frame (chạy trong FixedUpdate)
    public void UpdateBossAI(GameRoom room)
    {
        if (IsDead) return;

        // TODO: AI logic - nhắm về player gần nhất, tấn công, v.v
        // Ví dụ: Boss đi về phía player đầu tiên
        _targetPlayer = GetNearestPlayer(room.GetAllPlayerStates());
        if (_targetPlayer != null)
        { 

            if (_actionTimer < _actionInterval && DoneMoveToPlayer == true)
            {
                _actionTimer += 0.016f; // Giả sử gọi mỗi frame ~60fps
                return;
            }
            // _currentState = (EnemyActionState)new Random().Next(0, 5);
            _currentState = EnemyActionState.Move;
            _actionTimer = 0f;
            DoneMoveToPlayer = true;
            if (_currentState == EnemyActionState.Move)
            {
                DoneMoveToPlayer = false;
                Console.WriteLine($"[Boss {BossId}] Chuyển sang trạng thái: {_currentState}");
                MoveTowardsPlayer(_targetPlayer);
            }
            // else if (_currentState == EnemyActionState.Punch || _currentState == EnemyActionState.Strike)
            // {
            //     AttackPlayer(_targetPlayer);
            // }
            _ = room.BroadcastInWorldAsync(new S_BossStatePacket
            {
                BossType = Type,
                BossId = BossId,
                BossX = Position.X,
                BossY = Position.Y,
                HpCurrent = HpCurrent,
                HpMax = HpMax,
                AnimState = _currentState,
                speed = Speed
            });
        }

    }
    private async Task DefeatBossAsync(GameRoom room)
    {
        IsDead = true;
        var elapsedSeconds = (DateTime.Now - SpawnTime).TotalSeconds;

        // Tính reward dựa trên damage
        // var playerDamages = room.GetPlayerDamageStats(); // Hàm phụ để lấy damage stats

        await room.BroadcastAsync(new S_BossDefeatPacket
        {
            BossId = BossId,
            LastHitPlayerId = LastDamagePlayerId,
            ClearTimeMs = (long)(elapsedSeconds * 1000),
            TotalExpReward = 5000,
            TotalGoldReward = 2000
        });
    }

    private int CalculateBossDamage(int skillId, PlayerState playerState)
    {
        if (playerState == null)
        {
            return 0;
        }

        SkillInfo skillInfo = DataTest.GetSkillInfo(skillId);

        return skillInfo.DamegeType switch
        {
            DamegeType.Hp => (int)(skillInfo.percentage * playerState.HpMax),
            DamegeType.Ki => (int)(skillInfo.percentage * playerState.KiMax),
            DamegeType.Sd => (int)(skillInfo.percentage * playerState.SdMax),
            _ => 0
        };
    }

    private void MoveTowardsPlayer(PlayerState player)
    {
        float distance = Vector2.Distance(Position, new Vector2(player.X, player.Y));
        if (distance < 1.0f)
        {
            _currentState = EnemyActionState.Idle;
            DoneMoveToPlayer = true;
            return;
        }
        if (distance < 3.0f)
        {
            Vector2 direction = Vector2.Normalize(new Vector2(player.X, player.Y) - Position);
            Position += direction * Speed * 0.016f;
        }
        if (distance > 3f)
        {
            Position = new Vector2(player.X, player.Y);
        }
    }
    private void AttackPlayer(PlayerState player)
    {
        float distance = Vector2.Distance(Position, new Vector2(player.X, player.Y));
        if (distance < 1.5f)
        {
            _currentState = EnemyActionState.Punch;
        }
        else if (distance < 3.0f)
        {
            _currentState = EnemyActionState.Strike;
        }
    }
    private PlayerState GetNearestPlayer(List<PlayerState> players)
    {
        if (players.Count == 0)
        {
            return null!;
        }
        PlayerState nearest = null!;
        float minDistance = float.MaxValue;
        foreach (var player in players)
        {
            float distance = Vector2.Distance(Position, new Vector2(player.X, player.Y));
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = player;
            }
        }
        return nearest;
    }
}