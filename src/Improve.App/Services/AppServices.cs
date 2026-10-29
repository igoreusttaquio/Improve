using Improve.App.ViewModels;
using Improve.Core.Execution;
using Improve.Core.Providers;
using Improve.Core.Storage;
using Improve.Platform.Secrets;
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
        return services.BuildServiceProvider();
    }
}
