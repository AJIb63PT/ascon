namespace MiniPdm.Application.Cad;

/// <summary>
/// Компонент в составе сборки. Ссылка на дочерний документ ведётся по имени файла:
/// идентификатором документа в CAD-системе является имя файла на диске.
/// </summary>
/// <param name="FileName">Имя файла дочернего документа в той же папке.</param>
/// <param name="Quantity">Количество экземпляров.</param>
public sealed record CadComponent(string FileName, int Quantity);