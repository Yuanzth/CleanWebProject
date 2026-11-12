using System;
using Microsoft.Data.SqlClient;

string connStr = "Server=.\\DBMSALDO;Database=ITAssetTrack;User Id=sa;Password=Admin123;TrustServerCertificate=true;";

try
{
    using var conn = new SqlConnection(connStr);
    conn.Open();
    Console.WriteLine("Successfully connected to SQL Server!");

    // Check Employee NRP stats
    string queryCount = "SELECT COUNT(*) FROM tbl_m_employee";
    using (var cmd = new SqlCommand(queryCount, conn))
    {
        int count = (int)cmd.ExecuteScalar();
        Console.WriteLine($"Total Employees in tbl_m_employee: {count}");
    }

    string queryNrpCount = "SELECT COUNT(*) FROM tbl_m_employee WHERE NRP IS NOT NULL AND NRP <> '' AND NRP <> '-'";
    using (var cmd = new SqlCommand(queryNrpCount, conn))
    {
        int count = (int)cmd.ExecuteScalar();
        Console.WriteLine($"Employees with non-empty NRP: {count}");
    }

    // Top 5 employees
    string queryTop = "SELECT TOP 5 NRP, FullName FROM tbl_m_employee";
    using (var cmd = new SqlCommand(queryTop, conn))
    using (var reader = cmd.ExecuteReader())
    {
        Console.WriteLine("\n=== Top 5 Employees ===");
        while (reader.Read())
        {
            string nrp = reader.IsDBNull(0) ? "NULL" : reader.GetString(0);
            string name = reader.IsDBNull(1) ? "NULL" : reader.GetString(1);
            Console.WriteLine($"- NRP: [{nrp}] | Name: [{name}]");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
