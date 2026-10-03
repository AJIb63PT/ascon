namespace MiniPdm.Domain;

/// <summary>
/// Ошибка предметной области: нарушение инварианта или бизнес-правила.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Создаёт исключение с текстом сообщения.</summary>
    /// <param name="message">Описание нарушения.</param>
    public DomainException(string message) : base(message) { }
}

/// <summary>
/// Запрещённый переход между состояниями версии.
/// </summary>
public sealed class InvalidStateTransitionException : DomainException
{
    /// <summary>Создаёт исключение для запрещённого перехода.</summary>
    /// <param name="from">Исходное состояние.</param>
    /// <param name="to">Целевое состояние.</param>
    public InvalidStateTransitionException(VersionState from, VersionState to)
        : base($"Недопустимый переход состояния: {RussianName(from)} → {RussianName(to)}. " +
               "Обратных переходов нет, аннулированную версию изменить нельзя.")
    {
        From = from;
        To = to;
    }

    /// <summary>Исходное состояние.</summary>
    public VersionState From { get; }

    /// <summary>Целевое состояние.</summary>
    public VersionState To { get; }

    private static string RussianName(VersionState state) => state switch
    {
        VersionState.InWork => "В работе",
        VersionState.Approved => "Утверждено",
        VersionState.Annulled => "Аннулировано",
        _ => state.ToString(),
    };
}

/// <summary>
/// Попытка изменить версию, которая этого не допускает.
/// </summary>
public sealed class VersionNotMutableException : DomainException
{
    /// <summary>Создаёт исключение для неизменяемой версии.</summary>
    /// <param name="versionNo">Номер версии.</param>
    /// <param name="state">Состояние версии.</param>
    public VersionNotMutableException(int versionNo, VersionState state)
        : base($"Версия {versionNo} находится в состоянии «{state switch
        {
            VersionState.InWork => "В работе",
            VersionState.Approved => "Утверждено",
            VersionState.Annulled => "Аннулировано",
            _ => state.ToString(),
        }}» и не может быть изменена. Изменения вносятся в новую версию.")
    {
        VersionNo = versionNo;
        State = state;
    }

    /// <summary>Номер версии.</summary>
    public int VersionNo { get; }

    /// <summary>Состояние версии.</summary>
    public VersionState State { get; }
}