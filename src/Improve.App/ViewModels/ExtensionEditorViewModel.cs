using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Improve.App.Resources;
using Improve.Core.Execution;
using Improve.Core.Models;
using Improve.Core.Prompting;
using Improve.Core.Providers;
using Improve.Core.Storage;

namespace Improve.App.ViewModels;

/// <summary>Editor de uma extensão, com teste em streaming na própria janela.</summary>
public sealed partial class ExtensionEditorViewModel : ObservableObject
{
    private readonly ConfigState _config;
    private readonly ISecretStore _secrets;
    private readonly IProviderFactory _providers;
    private readonly ExtensionGenerator _generator;
    private readonly Extension? _original;
    private readonly Action _close;

    public ExtensionEditorViewModel(
        ConfigState config, ISecretStore secrets, IProviderFactory providers, ExtensionGenerator generator, Extension? original, Action close)
    {
        _config = config;
        _secrets = secrets;
        _providers = providers;
        _generator = generator;
        _original = original;
        _close = close;

        Providers = config.Settings.Providers;
        _name = original?.Name ?? "";
        _icon = original?.Icon ?? "wand-sparkles";
        _prompt = original?.Prompt ?? "";
        _model = original?.Model ?? "";
        _hotkey = original?.Hotkey;
        _mode = original?.Mode ?? OutputMode.Suggest;
        _treatAsCode = original?.TreatAsCode ?? false;
        _codeLanguage = CodeLanguageOption.All.FirstOrDefault(o => o.Id == original?.CodeLanguage) ?? CodeLanguageOption.All[0];
        _codeFont = CodeFontOptions.FirstOrDefault(o => o.Family == original?.CodeFontFamily) ?? CodeFontOptions[0];
        _codeFontSize = original?.CodeFontSize ?? 12;
        _provider = Providers.FirstOrDefault(p => p.Id == original?.ProviderId) ?? (original is null && Providers.Count > 0 ? Providers[0] : null);
        _sample = Strings.TestSampleDefault;
        _ = LoadModelsAsync(CancellationToken.None);
    }

    public bool IsNew => _original is null;
    public string Title => IsNew ? Strings.EditorNewTitle : Strings.EditorEditTitle;
    public IReadOnlyList<ProviderConfig> Providers { get; }
    public ObservableCollection<string> Models { get; } = [];

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private string _prompt;
    [ObservableProperty] private ProviderConfig? _provider;
    [ObservableProperty] private string _model;
    [ObservableProperty] private string? _modelsStatus;
    [ObservableProperty] private bool _modelsStatusIsError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReplace), nameof(IsSuggest), nameof(IsCopy))]
    private OutputMode _mode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestLanguage))]
    private bool _treatAsCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestLanguage), nameof(PreviewCode), nameof(PreviewLanguage))]
    private CodeLanguageOption _codeLanguage;

    public static IReadOnlyList<CodeLanguageOption> CodeLanguageOptions => CodeLanguageOption.All;

    public IReadOnlyList<CodeFontOption> CodeFontOptions { get; } = CodeFontOption.Available();

    public static IReadOnlyList<double> CodeFontSizes { get; } = [10, 11, 12, 13, 14, 15, 16, 18, 20];

    [ObservableProperty] private CodeFontOption _codeFont;
    [ObservableProperty] private double _codeFontSize;

    /// <summary>Trecho de exemplo da linguagem escolhida, para a prévia da fonte.</summary>
    public string PreviewCode => CodeSamples.For(PreviewLanguage);

    public string PreviewLanguage => CodeLanguage.Id ?? "csharp";

    /// <summary>Linguagem do realce no teste: a escolhida, ou a detectada no texto de exemplo.</summary>
    public string? TestLanguage => TreatAsCode ? CodeLanguage.Id ?? CodeLanguages.Detect(Sample) : null;

    [ObservableProperty] private string? _hotkey;
    [ObservableProperty] private string? _hotkeyError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestLanguage))]
    private string _sample;
    [ObservableProperty] private string _testOutput = "";
    [ObservableProperty] private string? _testError;
    [ObservableProperty] private string? _validationError;

    public bool IsReplace { get => Mode == OutputMode.Replace; set { if (value) Mode = OutputMode.Replace; } }
    public bool IsSuggest { get => Mode == OutputMode.Suggest; set { if (value) Mode = OutputMode.Suggest; } }
    public bool IsCopy { get => Mode == OutputMode.Copy; set { if (value) Mode = OutputMode.Copy; } }

    partial void OnProviderChanged(ProviderConfig? value) => _ = LoadModelsAsync(CancellationToken.None);

    partial void OnHotkeyChanged(string? value) => HotkeyError = ValidateHotkey(value);

    [RelayCommand]
    private async Task LoadModelsAsync(CancellationToken ct)
    {
        Models.Clear();
        ModelsStatus = null;
        if (Provider is not { } config)
            return;
        try
        {
            var key = await _secrets.GetAsync(config.Id, ct);
            var models = await _providers.Create(config, key).ListModelsAsync(ct);
            if (Provider != config)
                return;
            foreach (var model in models)
                Models.Add(model);
            ModelsStatus = string.Format(CultureInfo.CurrentCulture, Strings.ModelsLoaded, models.Count);
            ModelsStatusIsError = false;
        }
        catch (ProviderException ex)
        {
            ModelsStatus = ex.Message;
            ModelsStatusIsError = true;
        }
    }

    [RelayCommand]
    private void ClearHotkey() => Hotkey = null;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TestAsync(CancellationToken ct)
    {
        TestOutput = "";
        TestError = null;
        try
        {
            var session = ExtensionGenerator.CreateSession(Build(), _config.Settings.Providers, new Capture(Sample, null, null));
            TestOutput = await _generator.GenerateAsync(session, chunk => Dispatcher.UIThread.Post(() => TestOutput += chunk), ct);
        }
        catch (Exception ex) when (ex is ProviderException or ExtensionConfigurationException)
        {
            TestError = ex.Message;
        }
        catch (OperationCanceledException)
        {
            // Parado pelo usuário: mantém o que já chegou.
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationError = string.IsNullOrWhiteSpace(Name) ? Strings.NameRequired
            : string.IsNullOrWhiteSpace(Prompt) ? Strings.PromptRequired
            : HotkeyError;
        if (ValidationError is not null)
            return;

        var extension = Build();
        await _config.UpdateExtensionsAsync(list => IsNew
            ? list.Append(extension)
            : list.Select(e => e.Id == extension.Id ? extension : e));
        _close();
    }

    [RelayCommand]
    private void Cancel()
    {
        TestCancelCommand.Execute(null);
        _close();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_original is { } original)
            await _config.UpdateExtensionsAsync(list => list.Where(e => e.Id != original.Id));
        _close();
    }

    private Extension Build()
    {
        var hotkey = Core.Models.Hotkey.TryParse(Hotkey, out var parsed) ? parsed.ToString() : null;
        var values = new Extension
        {
            Name = Name.Trim(), Icon = Icon, Prompt = Prompt.Trim(), ProviderId = Provider?.Id, Model = Model.Trim(),
            Mode = Mode, Hotkey = hotkey, TreatAsCode = TreatAsCode, CodeLanguage = TreatAsCode ? CodeLanguage.Id : null,
            CodeFontFamily = CodeFont.Family, CodeFontSize = CodeFontSize,
        };
        return _original is null
            ? values with { Order = int.MaxValue }
            : _original with
            {
                Name = values.Name, Icon = values.Icon, Prompt = values.Prompt, ProviderId = values.ProviderId,
                Model = values.Model, Mode = values.Mode, Hotkey = values.Hotkey,
                TreatAsCode = values.TreatAsCode, CodeLanguage = values.CodeLanguage,
                CodeFontFamily = values.CodeFontFamily, CodeFontSize = values.CodeFontSize,
            };
    }

    private string? ValidateHotkey(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        if (!Core.Models.Hotkey.TryParse(text, out var hotkey) || !hotkey.IsValid)
            return Strings.HotkeyInvalid;
        var owner = HotkeyConflicts.ForExtension(hotkey, _original?.Id ?? Guid.Empty, _config.Settings, _config.Extensions.Extensions);
        return owner is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.HotkeyConflict, owner);
    }
}

/// <summary>Opção de linguagem no editor: "Automática" (<c>Id</c> nulo) ou uma linguagem da lista.</summary>
public sealed record CodeLanguageOption(string? Id, string Name)
{
    public static IReadOnlyList<CodeLanguageOption> All =>
        [new(null, Strings.CodeLanguageAuto), .. CodeLanguages.All.Select(l => new CodeLanguageOption(l.Id, l.Name))];
}

/// <summary>Fonte do código: a JetBrains Mono embutida (<c>Family</c> nulo) ou uma monoespaçada instalada.</summary>
public sealed record CodeFontOption(string? Family, string Name)
{
    // Monoespaçadas conhecidas; só aparecem as instaladas no sistema.
    private static readonly string[] Known =
    [
        "Cascadia Code", "Cascadia Mono", "Consolas", "Fira Code", "Source Code Pro", "SF Mono", "Menlo", "Monaco",
        "Ubuntu Mono", "DejaVu Sans Mono", "Liberation Mono", "Hack", "IBM Plex Mono", "Roboto Mono", "Courier New",
    ];

    public static IReadOnlyList<CodeFontOption> Available()
    {
        var installed = FontManager.Current.SystemFonts.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [new(null, "JetBrains Mono"), .. Known.Where(installed.Contains).Select(name => new CodeFontOption(name, name))];
    }
}

/// <summary>Trechos curtos para a prévia da fonte, por linguagem.</summary>
internal static class CodeSamples
{
    public static string For(string language) => language switch
    {
        "python" => "def total(itens, desconto=0.1):\n    # Soma com desconto aplicado\n    return sum(i.preco for i in itens) * (1 - desconto)",
        "javascript" or "typescript" => "const total = (itens, desconto = 0.1) =>\n  // Soma com desconto aplicado\n  itens.reduce((s, i) => s + i.preco, 0) * (1 - desconto);",
        "sql" => "-- Pedidos pagos do mês\nSELECT cliente, SUM(total) AS valor\nFROM pedidos\nWHERE status = 'pago'\nGROUP BY cliente;",
        "json" => "{\n  \"nome\": \"improve\",\n  \"versao\": 1,\n  \"ativo\": true\n}",
        _ => "public decimal Total(decimal desconto = 0.1m)\n{\n    // Soma com desconto aplicado\n    return Itens.Sum(i => i.Preco) * (1 - desconto);\n}",
    };
}
