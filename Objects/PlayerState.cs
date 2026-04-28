// using System.Numerics;

// public class PlayerState
// {
//     private readonly object _stateLock = new();

//     public int PlayerId { get; private set; }
//     // public string Class { get; } // ENUM
//     // public string Role { get; } // ENUM
//     public Vector2 Position = Vector2.Zero;
//     public Vector2 Velocity = Vector2.Zero;
//     public Vector2 InputDirection = Vector2.Zero;
//     public string AnimState;
//     public float Speed { get; set; } = 5f;
//     public PlayerState(int playerId, float X, float Y, float VelX, float VelY, string AnimState)
//     {
//         PlayerId = playerId;
//         Position = new Vector2(X, Y);
//         Velocity = new Vector2(VelX, VelY);
//         this.AnimState = AnimState;
//         // Class = @class;
//         // Role = role;
//         // Task.Run(async () =>
//         // {
//         //     while (true)
//         //     {
//         //         UpdatePosition();
//         //         var data = PacketSerializer.Serialize(new S_PlayerMovePacket
//         //         {
//         //             PlayerId = UserId,
//         //             X = Position.X,
//         //             Y = Position.Y
//         //         });
//         //         // Gửi dữ liệu di chuyển cho tất cả người chơi khác
//         //         await TcpServerService.sendPacketAsync(SessionManager.Instance.GetStream(SessionId), data, CancellationToken.None);
//         //         await Task.Delay(1000 / 60); // Cập nhật 60 lần mỗi giây
//         //     }
//         // });
//     }
//     public void SetInputDirection(float inputX, float inputY)
//     {
//         lock (_stateLock)
//         {
//             Vector2 direction = new Vector2(inputX, inputY);

//             // Nếu người chơi có bấm nút (direction lớn hơn 0)
//             if (direction.LengthSquared() > 0)
//             {
//                 // Normalize để đi chéo không bị nhanh hơn đi thẳng
//                 direction = Vector2.Normalize(direction);
//             }

//             // Cập nhật vận tốc hiện tại
//             InputDirection = direction;
//             Velocity = direction * Speed;
//             AnimState = direction == Vector2.Zero ? "Idle" : "Moving";
//         }
//     }
//     public void UpdatePosition(float deltaSeconds)
//     {
//         lock (_stateLock)
//         {
//             // Nếu không di chuyển thì không cần tính toán
//             if (Velocity == Vector2.Zero) return;

//             // Cập nhật tọa độ mới
//             Position += Velocity * deltaSeconds;
//         }
//     }

//     public Vector2 GetPosition()
//     {
//         lock (_stateLock)
//         {
//             return Position;
//         }
//     }
//     public void setPosition(float x, float y)
//     {
//         lock (_stateLock)
//         {
//             Position = new Vector2(x, y);
//         }
//     }
// }