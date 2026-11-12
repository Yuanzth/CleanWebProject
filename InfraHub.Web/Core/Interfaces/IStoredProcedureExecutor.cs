using System.Data;

namespace InfraHub.Core.Interfaces;

/// <summary>
/// Interface untuk menjalankan Stored Procedures via ADO.NET.
/// Digunakan hanya oleh API controllers dengan query berat.
/// CRUD biasa menggunakan LINQ via EF Core.
/// </summary>
public interface IStoredProcedureExecutor
{
    /// <summary>
    /// Menjalankan SP yang mengembalikan multiple rows.
    /// </summary>
    Task<IEnumerable<T>> ExecuteReaderAsync<T>(
        string procedureName,
        Dictionary<string, object?>? parameters = null,
        Func<IDataReader, T>? mapper = null) where T : class;

    /// <summary>
    /// Menjalankan SP yang mengembalikan scalar value.
    /// </summary>
    Task<object?> ExecuteScalarAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null);

    /// <summary>
    /// Menjalankan SP tanpa return value (INSERT/UPDATE/DELETE).
    /// </summary>
    Task<int> ExecuteNonQueryAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null);

    /// <summary>
    /// Executes a raw SQL query and maps the result to ExpandoObject.
    /// </summary>
    Task<IEnumerable<dynamic>> ExecuteSqlAsync(
        string sql,
        object[]? parameters = null);
}
