namespace MiniPdm.Application.Persistence;

/// <summary>
/// Связь «версия родителя → объект-потомок» в терминах хранилища.
/// </summary>
/// <param name="ParentVersionId">Версия объекта, в состав которого входит потомок.</param>
/// <param name="ChildObjectId">Объект-потомок; его актуальная версия берётся из <c>current_version_id</c>.</param>
/// <param name="Quantity">Количество экземпляров.</param>
public sealed record BomLinkRow(long ParentVersionId, long ChildObjectId, int Quantity);