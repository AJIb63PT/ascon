namespace MiniPdm.Domain;

/// <summary>
/// Правила перехода между состояниями версии.
/// </summary>
/// <remarks>
/// Разрешены переходы: «В работе» → «Утверждено», «В работе» → «Аннулировано»,
/// «Утверждено» → «Аннулировано». Обратных переходов нет.
/// </remarks>
public static class VersionStateRules
{
    /// <summary>Указывает, разрешён ли переход между состояниями.</summary>
    /// <param name="from">Исходное состояние.</param>
    /// <param name="to">Целевое состояние.</param>
    public static bool CanTransition(VersionState from, VersionState to) => (from, to) switch
    {
        (VersionState.InWork, VersionState.Approved) => true,
        (VersionState.InWork, VersionState.Annulled) => true,
        (VersionState.Approved, VersionState.Annulled) => true,
        _ => false,
    };

    /// <summary>
    /// Проверяет переход и выбрасывает исключение, если он запрещён.
    /// </summary>
    /// <param name="from">Исходное состояние.</param>
    /// <param name="to">Целевое состояние.</param>
    /// <exception cref="InvalidStateTransitionException">Переход запрещён правилами.</exception>
    public static void EnsureCanTransition(VersionState from, VersionState to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStateTransitionException(from, to);
        }
    }

    /// <summary>Возвращает состояния, в которые можно перейти из указанного.</summary>
    /// <param name="from">Исходное состояние.</param>
    public static IReadOnlyList<VersionState> AllowedTargets(VersionState from)
        => Enum.GetValues<VersionState>().Where(to => CanTransition(from, to)).ToArray();

    /// <summary>Указывает, участвует ли версия в расчётах.</summary>
    /// <param name="state">Состояние версии.</param>
    public static bool ParticipatesInCalculations(VersionState state) => state is not VersionState.Annulled;

    /// <summary>
    /// Указывает, можно ли изменять атрибуты версии в этом состоянии.
    /// Утверждённую версию изменять нельзя — только через создание новой.
    /// </summary>
    /// <param name="state">Состояние версии.</param>
    public static bool IsMutable(VersionState state) => state is VersionState.InWork;
}