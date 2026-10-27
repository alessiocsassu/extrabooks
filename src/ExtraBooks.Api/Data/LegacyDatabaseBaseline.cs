using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ExtraBooks.Api.Data;

// Procedura una tantum per lo schema originariamente creato con EnsureCreated.
public static class LegacyDatabaseBaseline
{
    public static async Task ApplyAsync(BooksDbContext db)
    {
        if ((await db.Database.GetAppliedMigrationsAsync()).Any())
            throw new InvalidOperationException("Il database ha già una cronologia delle migrazioni.");

        await db.Database.OpenConnectionAsync();
        try
        {
            var columns = await ReadRowsAsync(db, """
                SELECT CONCAT(COLUMN_NAME, '|', COLUMN_TYPE, '|', IS_NULLABLE, '|', EXTRA, '|',
                    COALESCE(CHARACTER_SET_NAME, ''), '|', COALESCE(COLUMN_DEFAULT, '<null>'))
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'books'
                """);
            string[] expectedColumns =
            [
                "Id|int|NO|auto_increment||<null>",
                "Title|varchar(160)|NO||utf8mb4|<null>",
                "Author|varchar(120)|NO||utf8mb4|<null>",
                "Status|varchar(20)|NO||utf8mb4|<null>",
                "CurrentPage|int|NO|||<null>",
                "TotalPages|int|YES|||<null>",
                "Rating|int|YES|||<null>",
                "Notes|varchar(2000)|YES||utf8mb4|<null>",
                "FinishedOn|datetime(6)|YES|||<null>",
                "CreatedAtUtc|datetime(6)|NO|||<null>"
            ];
            var indexes = await ReadRowsAsync(db, """
                SELECT CONCAT(INDEX_NAME, '|', COLUMN_NAME, '|', NON_UNIQUE, '|', SEQ_IN_INDEX)
                FROM information_schema.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'books'
                """);
            var tables = await ReadRowsAsync(db, """
                SELECT CONCAT(TABLE_NAME, '|', ENGINE) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME <> '__EFMigrationsHistory'
                """);

            if (!columns.Order().SequenceEqual(expectedColumns.Order()) ||
                !indexes.Order().SequenceEqual(new[] { "IX_books_Title|Title|1|1", "PRIMARY|Id|0|1" }.Order()) ||
                !tables.SequenceEqual(new[] { "books|InnoDB" }))
                throw new InvalidOperationException("Schema diverso dalla versione iniziale. Nessuna baseline applicata: controllare il database prima di procedere.");

            var initialMigration = db.Database.GetMigrations().First();
            var history = db.GetService<IHistoryRepository>();
            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(initialMigration, "9.0.0")));
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<List<string>> ReadRowsAsync(BooksDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
        return rows;
    }
}
