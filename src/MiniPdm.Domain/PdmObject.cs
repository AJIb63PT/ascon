namespace MiniPdm.Domain;

/// <summary>
/// Объект системы: сборка, деталь или стандартное изделие.
/// </summary>
public sealed class PdmObject
{
    /// <summary>Создаёт объект.</summary>
    /// <param name="id">Идентификатор в хранилище; 0 до сохранения.</param>
    /// <param name="objectType">Тип объекта.</param>
    /// <param name="designation">Обозначение; null у стандартных изделий.</param>
    /// <param name="name">Наименование.</param>
    /// <param name="currentVersionId">Идентификатор текущей версии; null до сохранения.</param>
    public PdmObject(long id, PdmObjectType objectType, string? designation, string name, long? currentVersionId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Наименование объекта обязательно");
        }

        if (objectType is PdmObjectType.StandardPart && !string.IsNullOrWhiteSpace(designation))
        {
            throw new DomainException(
                "У стандартного изделия обозначение отсутствует: изделие идентифицируется по наименованию");
        }

        if (objectType is not PdmObjectType.StandardPart && !DesignationRules.IsRequired(objectType))
        {
            throw new DomainException($"Для типа {objectType} обозначение обязательно");
        }

        ObjectType = objectType;
        Designation = string.IsNullOrWhiteSpace(designation) ? null : designation.Trim();
        Name = name.Trim();
        Id = id;
        CurrentVersionId = currentVersionId;
    }

    /// <summary>Идентификатор объекта.</summary>
    public long Id { get; private set; }

    /// <summary>Тип объекта.</summary>
    public PdmObjectType ObjectType { get; }

    /// <summary>Обозначение по ЕСКД; null у стандартных изделий.</summary>
    public string? Designation { get; }

    /// <summary>Наименование.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Идентификатор текущей версии — последней неаннулированной.
    /// Поле избыточно, но упрощает рекурсивный запрос по дереву состава.
    /// </summary>
    public long? CurrentVersionId { get; private set; }

    /// <summary>
    /// Обозначение, по которому объект однозначно идентифицируется для пользователя:
    /// обозначение при его наличии, иначе наименование.
    /// </summary>
    public string DisplayKey => Designation ?? Name;

    /// <summary>Создаёт объект, проверяя правила формирования обозначения.</summary>
    /// <param name="objectType">Тип объекта.</param>
    /// <param name="designation">Обозначение.</param>
    /// <param name="name">Наименование.</param>
    /// <param name="failureReason">Причина отказа при нарушении правил; null при успехе.</param>
    /// <returns>Объект либо null, если данные не прошли проверку.</returns>
    public static PdmObject? TryCreate(
        PdmObjectType objectType,
        string? designation,
        string? name,
        out string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            failureReason = "Не указано наименование";
            return null;
        }

        if (objectType is PdmObjectType.StandardPart)
        {
            failureReason = null;
            return new PdmObject(0, objectType, null, name!, null);
        }

        if (!DesignationRules.TryValidate(designation, out var reason))
        {
            failureReason = reason;
            return null;
        }

        failureReason = null;
        return new PdmObject(0, objectType, designation, name!, null);
    }

    /// <summary>Переименовывает объект.</summary>
    /// <param name="name">Новое наименование.</param>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Наименование объекта обязательно");
        }

        Name = name.Trim();
    }

    /// <summary>
    /// Назначает идентификаторы после сохранения в хранилище.
    /// </summary>
    /// <param name="id">Идентификатор объекта.</param>
    public void AssignId(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (Id != 0) throw new InvalidOperationException("Объекту уже назначен идентификатор");

        Id = id;
    }

    /// <summary>
    /// Обновляет указатель на текущую версию. Поддерживается при создании и аннулировании версий.
    /// </summary>
    /// <param name="versionId">Идентификатор текущей версии; null, если неаннулированных версий нет.</param>
    public void SetCurrentVersionId(long? versionId) => CurrentVersionId = versionId;
}