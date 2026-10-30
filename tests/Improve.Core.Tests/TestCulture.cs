using System.Globalization;
using System.Runtime.CompilerServices;

namespace Improve.Core.Tests;

internal static class TestCulture
{
    // Os testes conferem as mensagens em pt-BR (idioma neutro); o CI roda com a cultura do sistema em inglês.
    [ModuleInitializer]
    internal static void UsePortuguese()
    {
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
    }
}