using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Improve.Core.Models;
using Improve.Core.Resources;

namespace Improve.Core.Storage;

/// <summary>Lê e grava <c>settings.json</c> e <c>extensions.json</c> de forma atômica, migrando pelo campo <c>version</c>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001",
    Justification = "SemaphoreSlim só precisa de Dispose quando AvailableWaitHandle é usado.")]
public sealed class SettingsStore(string directory, TimeProvider? time = null)
{
    public const string SettingsFileName = "settings.json";
    public const string ExtensionsFileName = "extensions.json";

    // Migrações por versão de origem: a função recebe o JSON na versão N e devolve na versão N+1.
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> SettingsMigrations = [];
    private static readonly Dictionary<int, Func<JsonObject, JsonObject>> ExtensionsMigrations = [];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public string Directory { get; } = directory;

    public Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default) =>
        LoadAsync(SettingsFileName, CoreJsonContext.Default.AppSettings, AppSettings.CurrentVersion,
            SettingsMigrations, AppSettings.CreateDefault, ct);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default) =>
        SaveAsync(SettingsFileName, settings with { Version = AppSettings.CurrentVersion }, CoreJsonContext.Default.AppSettings, ct);

    /// <summary>Na primeira execução cria as extensões de exemplo, ligadas ao primeiro provedor configurado.</summary>
    public async Task<ExtensionsDocument> LoadExtensionsAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct).ConfigureAwait(false);
        var providerId = settings.Providers.Count > 0 ? settings.Providers[0].Id : null;
        return await LoadAsync(ExtensionsFileName, CoreJsonContext.Default.ExtensionsDocument, ExtensionsDocument.CurrentVersion,
            ExtensionsMigrations, () => ExtensionsDocument.CreateDefault(providerId), ct).ConfigureAwait(false);
    }

    public Task SaveExtensionsAsync(ExtensionsDocument document, CancellationToken ct = default) =>
        SaveAsync(ExtensionsFileName, document with { Version = ExtensionsDocument.CurrentVersion }, CoreJsonContext.Default.ExtensionsDocument, ct);

    private async Task<T> LoadAsync<T>(
        string fileName,
        JsonTypeInfo<T> typeInfo,
        int currentVersion,
        Dictionary<int, Func<JsonObject, JsonObject>> migrations,
        Func<T> createDefault,
        CancellationToken ct)
    {
        var path = Path.Combine(Directory, fileName);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
                return await WriteDefaultAsync(path, createDefault(), typeInfo, ct).ConfigureAwait(false);

            JsonObject json;
            try
            {
                json = JsonNode.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false)) as JsonObject
                    ?? throw new JsonException("A raiz não é um objeto.");
            }
            catch (JsonException)
            {
                // Arquivo corrompido: guarda uma cópia para não perder dados e recomeça do padrão.
                File.Move(path, $"{path}.corrompido-{_time.GetLocalNow():yyyyMMdd-HHmmss}", overwrite: true);
                return await WriteDefaultAsync(path, createDefault(), typeInfo, ct).ConfigureAwait(false);
            }

            var version = json["version"]?.GetValue<int>() ?? 1;
            if (version > currentVersion)
                throw new SettingsException(string.Format(CultureInfo.CurrentCulture, CoreStrings.SettingsNewerVersion, fileName));

            var migrated = version < currentVersion;
            for (; version < currentVersion; version++)
            {
                json = migrations[version](json);
                json["version"] = version + 1;
            }

            var value = json.Deserialize(typeInfo) ?? throw new JsonException($"{fileName} vazio.");
            if (migrated)
                await WriteAsync(path, value, typeInfo, ct).ConfigureAwait(false);
            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WriteAsync(Path.Combine(Directory, fileName), value, typeInfo, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<T> WriteDefaultAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken ct)
    {
        await WriteAsync(path, value, typeInfo, ct).ConfigureAwait(false);
        return value;
    }

    private static Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken ct) =>
        AtomicFile.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), ct);
}

/// <summary>Configuração ilegível pelo app. A mensagem é para a UI.</summary>
public sealed class SettingsException(string message) : Exception(message);
