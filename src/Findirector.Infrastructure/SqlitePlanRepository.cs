using Findirector.Application;
using Findirector.Domain;
using Microsoft.Data.Sqlite;

namespace Findirector.Infrastructure;

/// <summary>Хранит планы в SQLite. Деньги — целым числом копеек.</summary>
public sealed class SqlitePlanRepository : IPlanRepository
{
    private readonly string connectionString;

    public SqlitePlanRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Не указан файл базы данных.", nameof(databasePath));

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false, // соединение закрывается сразу, файл не остаётся занятым
        }.ToString();

        CreateSchema();
    }

    public void Save(FinancialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        using SqliteConnection connection = Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
            INSERT INTO Plans (Id, Name, OpeningCashKopecks)
            VALUES ($id, $name, $cash)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                OpeningCashKopecks = excluded.OpeningCashKopecks;
            """,
            ("$id", plan.Id.ToString()), ("$name", plan.Name), ("$cash", ToKopecks(plan.OpeningCash)));

        Execute(connection, transaction, "DELETE FROM MonthPlans WHERE PlanId = $id;",
            ("$id", plan.Id.ToString()));

        for (int i = 0; i < plan.Months.Count; i++)
        {
            MonthPlan month = plan.Months[i];
            Execute(connection, transaction, """
                INSERT INTO MonthPlans (PlanId, MonthNumber, Quantity, PriceKopecks,
                    UnitCostKopecks, FixedCostsKopecks, PaidNowPercent)
                VALUES ($id, $number, $quantity, $price, $unitCost, $fixedCosts, $percent);
                """,
                ("$id", plan.Id.ToString()), ("$number", i + 1), ("$quantity", month.Quantity),
                ("$price", ToKopecks(month.Price)), ("$unitCost", ToKopecks(month.UnitCost)),
                ("$fixedCosts", ToKopecks(month.FixedCosts)), ("$percent", month.PaidNowPercent));
        }

        transaction.Commit(); // без Commit всё откатится при выходе из using
    }

    public IReadOnlyList<PlanSummary> List()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM Plans ORDER BY Name, Id;";

        var result = new List<PlanSummary>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(new PlanSummary(Guid.Parse(reader.GetString(0)), reader.GetString(1)));
        return result;
    }

    public FinancialPlan? Get(Guid id)
    {
        using SqliteConnection connection = Open();

        string name;
        long cashKopecks;
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Name, OpeningCashKopecks FROM Plans WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            name = reader.GetString(0);
            cashKopecks = reader.GetInt64(1);
        }

        var months = new List<MonthPlan>();
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Quantity, PriceKopecks, UnitCostKopecks, FixedCostsKopecks, PaidNowPercent
                FROM MonthPlans WHERE PlanId = $id ORDER BY MonthNumber;
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                months.Add(new MonthPlan(reader.GetInt32(0), FromKopecks(reader.GetInt64(1)),
                    FromKopecks(reader.GetInt64(2)), FromKopecks(reader.GetInt64(3)), reader.GetInt32(4)));
            }
        }

        // Конструктор заново проверит все правила, так что испорченные данные не пройдут
        return new FinancialPlan(id, name, FromKopecks(cashKopecks), months);
    }

    private void CreateSchema()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Plans (
                Id                 TEXT    NOT NULL PRIMARY KEY,
                Name               TEXT    NOT NULL CHECK (length(trim(Name)) > 0),
                OpeningCashKopecks INTEGER NOT NULL CHECK (OpeningCashKopecks >= 0)
            );

            CREATE TABLE IF NOT EXISTS MonthPlans (
                PlanId            TEXT    NOT NULL REFERENCES Plans (Id) ON DELETE CASCADE,
                MonthNumber       INTEGER NOT NULL CHECK (MonthNumber BETWEEN 1 AND 3),
                Quantity          INTEGER NOT NULL CHECK (Quantity >= 0),
                PriceKopecks      INTEGER NOT NULL CHECK (PriceKopecks >= 0),
                UnitCostKopecks   INTEGER NOT NULL CHECK (UnitCostKopecks >= 0),
                FixedCostsKopecks INTEGER NOT NULL CHECK (FixedCostsKopecks >= 0),
                PaidNowPercent    INTEGER NOT NULL CHECK (PaidNowPercent BETWEEN 0 AND 100),
                PRIMARY KEY (PlanId, MonthNumber)
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        // SQLite проверяет внешние ключи, только если это включить для каждого подключения
        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction,
        string sql, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    // Суммы в Domain всегда с точностью до копейки, поэтому умножение на 100 даёт целое число
    private static long ToKopecks(decimal amount) => decimal.ToInt64(amount * 100m);

    private static decimal FromKopecks(long kopecks) => kopecks / 100m;
}
