namespace MiniPdm.Domain;

/// <summary>
/// Состояние версии объекта. Соответствует состояниям раздела «Предметная область» задания.
/// </summary>
public enum VersionState
{
    /// <summary>«В работе»: версия может изменяться.</summary>
    InWork = 0,

    /// <summary>«Утверждено»: версия неизменяема, правки только через новую версию.</summary>
    Approved = 1,

    /// <summary>«Аннулировано»: версия не участвует в расчётах.</summary>
    Annulled = 2,
}