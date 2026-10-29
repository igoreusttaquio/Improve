using Improve.App.ViewModels;
using Improve.Core.Execution;
using Improve.Core.Providers;
using Improve.Core.Storage;
using Improve.Platform;
using Improve.Platform.Secrets;
using Improve.Platform.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Improve.App.Services;

public static class AppServices
{
    /// <summary>Monta o contêiner e carrega a configuração antes de a UI subir.</summary>
    public static async Task<ServiceProvider> BuildAsync(CancellationToken ct = default)
    {
        var configDirectory = AppPaths.ConfigDirectory();
        var config = new ConfigState(new SettingsStore(configDirectory));
        await config.LoadAsync(ct).ConfigureAwait(false);
        var secrets = await SecretStoreFactory.CreateAsync(configDirectory, ct).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddSingleton(secrets);
        services.AddSingleton(_ => ProviderFactory.CreateHttpClient());
        services.AddSingleton<IProviderFactory>(sp => new ProviderFactory(sp.GetRequiredService<HttpClient>()));
        services.AddSingleton<ExtensionGenerator>();
        services.AddTransient<SettingsViewModel>();
        AddPlatform(services);
        return services.BuildServiceProvider();
    }

    // Integrações por SO. macOS e Linux entram nas fases 5 e 6; até lá, só a janela de configurações funciona neles.
    private static void AddPlatform(ServiceCollection services)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var input = new SharpHookInput(WindowsFocusService.IsOwnWindowFocused);
        services.AddSingleton<IHotkeyService>(input);
        services.AddSingleton<IKeyboard>(input);
        services.AddSingleton<IClipboard, WindowsClipboard>();
        services.AddSingleton<IFocusService, WindowsFocusService>();
        services.AddSingleton<ICursorService, WindowsCursorService>();
        services.AddSingleton<IAutostartService, WindowsAutostart>();
        services.AddSingleton<ISelectionService>(sp =>
            new ClipboardSelectionService(sp.GetRequiredService<IClipboard>(), sp.GetRequiredService<IKeyboard>()));
        services.AddSingleton<ExtensionRunner>();
        services.AddSingleton<FlowController>();
    }
}
