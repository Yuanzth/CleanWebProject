using System.Data;
using System.Dynamic;
using InfraHub.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace InfraHub.Infrastructure.Data;

/// <summary>
/// Implementasi IStoredProcedureExecutor menggunakan ADO.NET langsung.
/// Digunakan untuk query berat yang membutuhkan Stored Procedure.
/// </summary>
public class StoredProcedureExecutor : IStoredProcedureExecutor
{
    private readonly string _connectionString;

    public StoredProcedureExecutor(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<IEnumerable<T>> ExecuteReaderAsync<T>(
        string procedureName,
        Dictionary<string, object?>? parameters = null,
        Func<IDataReader, T>? mapper = null) where T : class
    {
        var results = new List<T>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };

        AddParameters(command, parameters);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (mapper != null)
            {
                results.Add(mapper(reader));
            }
            else
            {
                // Auto-map to ExpandoObject when no mapper is provided (for dynamic calls)
                var expando = new ExpandoObject() as IDictionary<string, object>;
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    expando[columnName] = value!;
                }
                results.Add((T)(object)expando);
            }
        }

        return results;
    }

    public async Task<object?> ExecuteScalarAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };

        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync();
    }

    public async Task<int> ExecuteNonQueryAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };

        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqlCommand command, Dictionary<string, object?>? parameters)
    {
        if (parameters == null) return;

        foreach (var (key, value) in parameters)
        {
            var paramName = key.StartsWith("@") ? key : $"@{key}";
            command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }

    public async Task<IEnumerable<dynamic>> ExecuteSqlAsync(string sql, object[]? parameters = null)
    {
        var results = new List<dynamic>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = 120
        };

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
            }
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var expando = new ExpandoObject() as IDictionary<string, object>;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                expando[columnName] = value!;
            }
            results.Add((dynamic)expando);
        }

        return results;
    }
}
