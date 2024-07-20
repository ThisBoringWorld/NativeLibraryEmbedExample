using System.Data.Common;
using Microsoft.Data.Sqlite;
using NativeLibraryEmbedExample;

EmbeddedUnmanagedDllResolver.Default()
                            .Add("e_sqlite3", resourceName => resourceName.Contains("e_sqlite3"))
                            .ResolvingDefault();

using DbConnection connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();

using var createTableCommand = connection.CreateCommand();
createTableCommand.CommandText = """
                                 CREATE TABLE `test_table` (
                                   "id" INTEGER NOT NULL,
                                   "value" text NOT NULL,
                                   "status" integer NOT NULL,
                                   PRIMARY KEY ("id")
                                 );
                                 """;

await createTableCommand.ExecuteNonQueryAsync();

using var insertCommand = connection.CreateCommand();
insertCommand.CommandText = """
                            INSERT INTO `test_table` ("id", "value", "status") VALUES (1, 'c73f6558c9c641e6bf88d67e845e91f3', 100);
                            INSERT INTO `test_table` ("id", "value", "status") VALUES (2, 'f824da2d1ddc4eb09efd8dbde317afcd', 200);
                            """;

await insertCommand.ExecuteNonQueryAsync();

using var queryCommand = connection.CreateCommand();
queryCommand.CommandText = "SELECT id, value, `status` FROM `test_table`;";

using var reader = await queryCommand.ExecuteReaderAsync();

while (await reader.ReadAsync())
{
    Console.WriteLine($"{reader.GetInt32(0)} - {reader.GetString(1)} - {reader.GetInt32(2)}");
}

await reader.CloseAsync();

Console.WriteLine("Finished");
Console.ReadLine();
