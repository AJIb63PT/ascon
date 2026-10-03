namespace MiniPdm.Domain;

/// <summary>
/// Связь состава «Состоит из …»: версия сборки-родителя содержит дочерний объект в количестве.
/// </summary>
/// <remarks>
/// Связь ведёт на <em>объект</em>, а не на его версию. При расчётах берётся текущая версия
/// дочернего объекта.
/// </remarks>
public sealed class BomLink
{
    /// <summary>Создаёт связь состава.</summary>
    /// <param name="id">Идентификатор связи; 0 до сохранения.</param>
    /// <param name="parentVersionId">Идентификатор версии сборки-родителя.</param>
    /// <param name="childObjectId">Идентификатор дочернего объекта.</param>
    /// <param name="quantity">Количество, строго больше нуля.</param>
    /// <exception cref="DomainException">Количество не положительное.</exception>
    public BomLink(long id, long parentVersionId, long childObjectId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException(
                $"Количество компонента должно быть больше нуля, получено {quantity}");
        }

        if (parentVersionId == childObjectId)
        {
            throw new DomainException("Объект не может входить в собственный состав");
        }

        Id = id;
        ParentVersionId = parentVersionId;
        ChildObjectId = childObjectId;
        Quantity = quantity;
    }

    /// <summary>Идентификатор связи.</summary>
    public long Id { get; private set; }

    /// <summary>Идентификатор версии сборки-родителя.</summary>
    public long ParentVersionId { get; }

    /// <summary>Идентификатор дочернего объекта.</summary>
    public long ChildObjectId { get; }

    /// <summary>Количество экземпляров.</summary>
    public int Quantity { get; private set; }

    /// <summary>Изменяет количество.</summary>
    /// <param name="quantity">Новое количество.</param>
    public void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException(
                $"Количество компонента должно быть больше нуля, получено {quantity}");
        }

        Quantity = quantity;
    }

    /// <summary>Назначает идентификатор после сохранения в хранилище.</summary>
    /// <param name="id">Идентификатор связи.</param>
    public void AssignId(long id)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
    }
}