namespace DragonBall_Server.Configs;
using MySqlConnector;
public class MyConnection
{
    private readonly string _connectionString;

    public MyConnection(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' not found.");
    }

    public MySqlConnection GetOpenConnection()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
    public void ClosedConnection(MySqlConnection connection)
    {
        if (connection != null && connection.State == System.Data.ConnectionState.Open)
        {
            connection.Close();
        }
    }
}