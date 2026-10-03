namespace MiniPdm.Infrastructure.Persistence;

/// <summary>
/// Провайдер СУБД.
/// </summary>
public enum DbProvider
{
    /// <summary>PostgreSQL — основной вариант поставки.</summary>
    PostgreSql = 0,

    /// <summary>SQLite — автономный режим и режим автономных тестов.</summary>
    Sqlite = 1,
}

/// <summary>
/// Диалект СУБД. Изолирует различия SQL, чтобы репозитории оставались диалектно-независимыми.
/// </summary>
/// <remarks>
/// Различия между PostgreSQL и SQLite сведены к минимуму: идентификаторы получаются
/// одинаково (<c>INSERT ... RETURNING</c>), параметры передаются одинаково (<c>@name</c>),
/// рекурсивные CTE поддерживаются обоими. Различаются только DDL и представление дат.
/// </remarks>
public interface IDbDialect
{
    /// <summary>Идентификатор провайдера.</summary>
    DbProvider Provider { get; }

    /// <summary>Имя ресурса со скриптом схемы.</summary>
    string SchemaResourceName { get; }

    /// <summary>
    /// Возвращает текст скрипта схемы. Разделитель команд — <c>;</c> на отдельной строке.
    /// </summary>
    string GetSchemaScript();
}