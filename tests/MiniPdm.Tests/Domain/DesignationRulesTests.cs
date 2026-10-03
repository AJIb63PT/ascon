using MiniPdm.Domain;

namespace MiniPdm.Tests.Domain;

/// <summary>
/// Проверка правил обозначений ЕСКД.
/// </summary>
public sealed class DesignationRulesTests
{
    [Theory]
    [InlineData("РДЦЛ.304112.601")]
    [InlineData("АБВГ.111111.001")]
    [InlineData("ЕСКД.000000.000")]
    public void ValidDesignations_AreAccepted(string designation)
    {
        Assert.True(DesignationRules.IsValid(designation));
        Assert.True(DesignationRules.TryValidate(designation, out _));
    }

    [Theory]
    [InlineData("РДЦЛ.30411.601")]     // пять цифр вместо шести
    [InlineData("РДЦЛ.3041121.601")]   // семь цифр
    [InlineData("РДЦЛ.304112.60")]     // две цифры вместо трёх
    [InlineData("PДЦЛ.304112.601")]    // латинская P вместо кириллической Р
    [InlineData("РДЦ.304112.601")]     // три буквы вместо четырёх
    [InlineData("РДЦЛЛ.304112.601")]   // пять букв
    [InlineData("РДЦЛ-304112-601")]    // дефис вместо точки
    [InlineData("РДЦЛ.304112.601 ")]   // пробел в конце
    [InlineData("")]
    public void InvalidDesignations_AreRejected(string designation)
    {
        Assert.False(DesignationRules.IsValid(designation));
        Assert.False(DesignationRules.TryValidate(designation, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void DesignationIsRequiredForAssemblyAndPart()
    {
        Assert.True(DesignationRules.IsRequired(PdmObjectType.Assembly));
        Assert.True(DesignationRules.IsRequired(PdmObjectType.Part));
        Assert.False(DesignationRules.IsRequired(PdmObjectType.StandardPart));
    }

    [Fact]
    public void TryValidate_ExplainsTheReason()
    {
        const string designation = "PДЦЛ.304112.601";

        Assert.False(DesignationRules.TryValidate(designation, out var reason));
        Assert.Contains(designation, reason);
    }
}