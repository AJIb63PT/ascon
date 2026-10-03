using MiniPdm.Application.Cad;
using MiniPdm.Tests.Fakes;

namespace MiniPdm.Tests;

/// <summary>
/// Построители документов выгрузки для тестов.
/// </summary>
public static class Cad
{
    public static CadDocument Assembly(
        string fileName,
        string designation,
        string name,
        params CadComponent[] components) =>
        new()
        {
            FormatVersion = 1,
            FileName = fileName,
            Type = CadDocumentType.Assembly,
            Designation = designation,
            Name = name,
            Components = components,
        };

    public static CadDocument Part(
        string fileName,
        string designation,
        string name,
        decimal? massKg = 1m,
        string? material = "Сталь") =>
        new()
        {
            FormatVersion = 1,
            FileName = fileName,
            Type = CadDocumentType.Part,
            Designation = designation,
            Name = name,
            MassKg = massKg,
            Material = material,
        };

    public static CadDocument StandardPart(string fileName, string name, decimal? massKg = 0.05m) =>
        new()
        {
            FormatVersion = 1,
FileName = fileName,
            Type = CadDocumentType.StandardPart,
            Name = name,
            MassKg = massKg,
        };

    public static CadComponent Component(string fileName, int quantity = 1) => new(fileName, quantity);

    /// <summary>
    /// Корректная сборка «Гайка» из двух одинаковых болтов и шайбы —
    /// используется как эталон для расчётов.
    /// </summary>
    public const string AssemblyFile = "Гайка.a3d";
    public const string BoltFile = "Болт.m3d";
    public const string WasherFile = "Шайба.m3d";

    public static FakeCadDocumentCatalog DefaultCatalog() =>
        new FakeCadDocumentCatalog()
            .Add(AssemblyFile)
            .Add(BoltFile)
            .Add(WasherFile);

    public static FakeCadDocumentReader DefaultReader() =>
        new FakeCadDocumentReader()
            .WithDocument(Assembly(
                AssemblyFile,
                "АБВГ.111111.001",
                "Гайка",
                Component(BoltFile, 2),
                Component(WasherFile, 2)))
            .WithDocument(Part(BoltFile, "АБВГ.111111.002", "Болт", massKg: 0.01m, material: "Сталь 40"))
            .WithDocument(StandardPart(WasherFile, "Шайба ГОСТ 11371-78", massKg: 0.002m));
}