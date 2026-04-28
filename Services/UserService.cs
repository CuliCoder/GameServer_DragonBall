using DragonSiege.Models;
using MySqlConnector;
public class UserService
{
    private readonly MyConnection _myConnection;
    private readonly HashPass HashPass ;
    public UserService(MyConnection myConnection, HashPass hashPass)
    {
        _myConnection = myConnection;
        HashPass = hashPass;
    }

    public List<User> GetAll()
    {
        List<User> users = new List<User>();
        try
        {
            using MySqlConnection connection = _myConnection.GetOpenConnection();
            MySqlCommand command = new MySqlCommand("SELECT * FROM users", connection);
            using MySqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                User user = new User
                {
                    Id = reader.GetInt32("id"),
                    Username = reader.GetString("username"),
                    Email = reader.GetString("email"),
                    PasswordHash = reader.GetString("password_hash"),
                    Class = reader.GetString("class"),
                    Role = reader.GetString("role"),
                    IsBanned = reader.GetBoolean("is_banned"),
                    CreatedAt = reader.GetDateTime("created_at")
                };
                users.Add(user);
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        return users;
    }
    public User? GetById(int id)
    {
        User? user = null;
        try
        {
            using MySqlConnection connection = _myConnection.GetOpenConnection();
            MySqlCommand command = new MySqlCommand("SELECT * FROM users WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            using MySqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                user = new User
                {
                    Id = reader.GetInt32("id"),
                    Username = reader.GetString("username"),
                    Email = reader.GetString("email"),
                    PasswordHash = reader.GetString("password_hash"),
                    Class = reader.GetString("class"),
                    Role = reader.GetString("role"),
                    IsBanned = reader.GetBoolean("is_banned"),
                    CreatedAt = reader.GetDateTime("created_at")
                };
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
        return user;
    }
    public bool Add(User user)
    {
        try
        {
            using MySqlConnection connection = _myConnection.GetOpenConnection();
            MySqlCommand command = new MySqlCommand("INSERT INTO users (username, email, password_hash, class, role, is_banned, created_at) VALUES (@username, @email, @password_hash, @class, @role, @is_banned, @created_at)", connection);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@password_hash", HashPass.HashPassword(user.PasswordHash ?? string.Empty));
            command.Parameters.AddWithValue("@class", user.Class);
            command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@is_banned", user.IsBanned);
            command.Parameters.AddWithValue("@created_at", user.CreatedAt ?? DateTime.Now);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
    public bool Update(User user)
    {
        try
        {
            using MySqlConnection connection = _myConnection.GetOpenConnection();
            MySqlCommand command = new MySqlCommand("UPDATE users SET username = @username, email = @email, password_hash = @password_hash, class = @class, role = @role, is_banned = @is_banned WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", user.Id);
            command.Parameters.AddWithValue("@username", user.Username);
            command.Parameters.AddWithValue("@email", user.Email);
            command.Parameters.AddWithValue("@password_hash", HashPass.HashPassword(user.PasswordHash ?? string.Empty));
            command.Parameters.AddWithValue("@class", user.Class);
            command.Parameters.AddWithValue("@role", user.Role);
            command.Parameters.AddWithValue("@is_banned", user.IsBanned);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
    public bool Delete(int id)
    {
        try
        {
            using MySqlConnection connection = _myConnection.GetOpenConnection();
            MySqlCommand command = new MySqlCommand("DELETE FROM users WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", id);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }
}