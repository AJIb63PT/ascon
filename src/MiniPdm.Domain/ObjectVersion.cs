namespace MiniPdm.Domain;

/// <summary>
/// Версия объекта: неизменяемый по требованию снимок атрибутов.
/// </summary>
public sealed class ObjectVersion
{
    /// <summary>Создаёт версию объекта.</summary>
    /// <param name="id">Идентификатор версии в хранилище; 0 до сохранения.</param>
    /// <param name="objectId">Идентификатор объекта-владельца.</param>
    /// <param name="versionNo">Номер версии, начиная с 1.</param>
    /// <param name="state">Состояние версии.</param>
    /// <param name="material">Материал; не задан для сборок и стандартных изделий.</param>
    /// <param name="massKg">Масса в килограммах за одну штуку; не задана для сборок.</param>
    /// <param name="createdAt">Момент создания версии.</param>
    public ObjectVersion(
        long id,
        long objectId,
        int versionNo,
        VersionState state,
        string? material,
        decimal? massKg,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);

        Id = id;
        ObjectId = objectId;
        VersionNo = versionNo;
        State = state;
        Material = Normalize(material);
        MassKg = massKg;
        CreatedAt = createdAt;
    }

    /// <summary>Идентификатор версии.</summary>
    public long Id { get; }

    /// <summary>Идентификатор объекта-владельца.</summary>
    public long ObjectId { get; }

    /// <summary>Номер версии, начиная с 1.</summary>
    public int VersionNo { get; }

    /// <summary>Состояние версии.</summary>
    public VersionState State { get; private set; }

    /// <summary>Материал изделия.</summary>
    public string? Material { get; private set; }

    /// <summary>Масса в килограммах за одну штуку.</summary>
    public decimal? MassKg { get; private set; }

    /// <summary>Момент создания версии.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Указывает, может ли версия изменяться.</summary>
    public bool IsMutable => VersionStateRules.IsMutable(State);

    /// <summary>
    /// Применяет атрибуты к версии. Допускается только для версии «В работе».
    /// </summary>
    /// <param name="material">Новый материал.</param>
    /// <param name="massKg">Новая масса.</param>
    /// <exception cref="VersionNotMutableException">Версия не находится в состоянии «В работе».</exception>
    public void UpdateAttributes(string? material, decimal? massKg)
    {
        EnsureMutable();
        Material = Normalize(material);
        MassKg = massKg;
    }

    /// <summary>
    /// Меняет состояние версии согласно разрешённым переходам.
    /// </summary>
    /// <param name="newState">Целевое состояние.</param>
    /// <exception cref="InvalidStateTransitionException">Переход запрещён.</exception>
    public void ChangeState(VersionState newState)
    {
        if (State == newState) return;

        if (!VersionStateRules.CanTransition(State, newState))
        {
            throw new InvalidStateTransitionException(State, newState);
        }

        State = newState;
    }

    /// <summary>Создаёт копию версии с новым идентификатором и номером.</summary>
    /// <param name="id">Новый идентификатор.</param>
    /// <param name="versionNo">Новый номер версии.</param>
    /// <param name="state">Состояние новой версии.</param>
    /// <param name="createdAt">Момент создания.</param>
    public ObjectVersion Fork(long id, int versionNo, VersionState state, DateTimeOffset createdAt)
        => new(id, ObjectId, versionNo, state, Material, MassKg, createdAt);

    private void EnsureMutable()
    {
        if (!IsMutable) throw new VersionNotMutableException(VersionNo, State);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}