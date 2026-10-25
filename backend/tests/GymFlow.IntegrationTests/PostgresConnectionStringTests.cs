using GymFlow.Infrastructure.Persistence;
using Npgsql;

namespace GymFlow.IntegrationTests;

public class PostgresConnectionStringTests
{
    [Fact]
    public void Hosted_database_urls_are_turned_into_npgsql_connection_strings()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(
            "postgresql://gym_owner:p%40ss%3Aword@ep-cool-name-123.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        Assert.Equal("ep-cool-name-123.eu-central-1.aws.neon.tech", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("neondb", result.Database);
        Assert.Equal("gym_owner", result.Username);
        Assert.Equal("p@ss:word", result.Password);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void Regular_connection_strings_are_left_alone()
    {
        const string value = "Host=localhost;Port=5433;Database=gymflow;Username=gymflow;Password=gymflow";
        Assert.Equal(value, PostgresConnectionString.Normalize(value));
    }

    [Fact]
    public void Custom_ports_are_kept()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize("postgres://u:p@db.example.com:6543/app"));
        Assert.Equal((6543, SslMode.Prefer), (result.Port, result.SslMode));
    }
}
