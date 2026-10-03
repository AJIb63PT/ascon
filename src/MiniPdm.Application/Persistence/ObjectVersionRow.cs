using MiniPdm.Domain;

namespace MiniPdm.Application.Persistence;

/// <summary>
/// Версия объекта в терминах хранилища.
/// </summary>
/// <param name="Id">Идентификатор версии.</param>
/// <param name="ObjectId">Идентификатор объекта.</param>
/// <param name="VersionNo">Номер версии, начиная с 1.</param>
/// <param name="State">Состояние версии.</param>
/// <param name="Material">Материал.</param>
/// <param name="MassKg">Масса в килограммах за один экземпляр.</param>
/// <param name="CreatedAt">Время создания.</param>
public sealed record ObjectVersionRow(
    long Id,
    long ObjectId,
    int VersionNo,
    VersionState State,
    string? Material,
    decimal? MassKg,
    DateTimeOffset CreatedAt);