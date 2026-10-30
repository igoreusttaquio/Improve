using System.Globalization;
using Improve.Core.Models;
using Improve.Core.Providers;
using Improve.Core.Resources;

namespace Improve.Core.Tests.Models;

public class LocalizationTests
{
    [Theory]
    [InlineData(LanguagePreference.System, "pt-PT", "pt-BR")]
    [InlineData(LanguagePreference.System, "es-MX", "es")]
    [InlineData(LanguagePreference.System, "fr-FR", "en")]
    [InlineData(LanguagePreference.System, "en-US", "en")]
    [InlineData(LanguagePreference.English, "pt-BR", "en")]
    [InlineData(LanguagePreference.Spanish, "en-US", "es")]
    [InlineData(LanguagePreference.PortugueseBrazil, "es-ES", "pt-BR")]
    public void Resolves_supported_culture(LanguagePreference preference, string system, string expected) =>
        Assert.Equal(expected, Localization.Resolve(preference, CultureInfo.GetCultureInfo(system)).Name);

    [Theory]
    [InlineData("en", "Invalid API key for Anthropic. Open Settings > Providers.")]
    [InlineData("es", "Clave de API no válida para Anthropic. Abre Configuración > Proveedores.")]
    [InlineData("pt-BR", "Chave de API inválida para Anthropic. Abra Configurações > Provedores.")]
    public void Provider_errors_follow_the_interface_language(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.Equal(expected, new ProviderException(ProviderErrorKind.InvalidKey, "Anthropic").Message);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void Example_extensions_follow_the_interface_language()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            Assert.Equal(CoreStrings.ExampleImproveName, ExtensionsDocument.CreateDefault(null).Extensions[0].Name);
            Assert.Equal("Improve message", CoreStrings.ExampleImproveName);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}