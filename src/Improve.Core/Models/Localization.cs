using System.Globalization;
using System.Text.Json.Serialization;

namespace Improve.Core.Models;

public enum LanguagePreference
{
    [JsonStringEnumMemberName("system")]
    System,
    [JsonStringEnumMemberName("pt-BR")]
    PortugueseBrazil,
    [JsonStringEnumMemberName("en")]
    English,
    [JsonStringEnumMemberName("es")]
    Spanish,
}

/// <summary>Idiomas da interface: português (Brasil), inglês e espanhol.</summary>
public static class Localization
{
    /// <summary>Cultura da interface. "Seguir o sistema" usa o idioma do SO quando suportado; senão, inglês.</summary>
    public static CultureInfo Resolve(LanguagePreference preference, CultureInfo system) => preference switch
    {
        LanguagePreference.PortugueseBrazil => CultureInfo.GetCultureInfo("pt-BR"),
        LanguagePreference.English => CultureInfo.GetCultureInfo("en"),
        LanguagePreference.Spanish => CultureInfo.GetCultureInfo("es"),
        _ => system.TwoLetterISOLanguageName switch
        {
            "pt" => CultureInfo.GetCultureInfo("pt-BR"),
            "es" => CultureInfo.GetCultureInfo("es"),
            _ => CultureInfo.GetCultureInfo("en"),
        },
    };

    /// <summary>Aplica a cultura da interface a todas as threads (inclusive as que já existem sem cultura própria).</summary>
    public static CultureInfo Apply(LanguagePreference preference)
    {
        var culture = Resolve(preference, CultureInfo.InstalledUICulture);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return culture;
    }
}
