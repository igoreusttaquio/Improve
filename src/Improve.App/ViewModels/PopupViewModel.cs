using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Improve.App.Resources;
using Improve.Core.Models;

namespace Improve.App.ViewModels;

public enum PopupState
{
    Hidden,
    Actions,
    Processing,
    Suggestion,
    Toast,
}

/// <summary>Linha do ActionPopup.</summary>
public sealed partial class PopupItem(Extension extension, int number, string? label = null) : ObservableObject
{
    public Extension Extension { get; } = extension;
    public string Name => label ?? Extension.Name;

    /// <summary>Prompt livre digitado na busca, e não uma extensão salva.</summary>
    public bool IsFreePrompt => label is not null;
    public string Icon => Extension.Icon;

    /// <summary>Atalho de 1 a 9 pela posição na lista completa; vazio a partir da 10ª.</summary>
    public string Number { get; } = number is >= 1 and <= 9 ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Estado do popup flutuante: lista de extensões, indicador de processamento, painel de sugestão ou aviso.
/// As ações são executadas por quem cria o ViewModel (o <c>FlowController</c>).
/// </summary>
public sealed partial class PopupViewModel : ObservableObject
{
    private IReadOnlyList<PopupItem> _all = [];

    /// <summary>De onde o prompt livre herda provedor e modelo (ver <c>FlowController.FreePromptBase</c>).</summary>
    private Extension? _freePromptBase;

    public PopupViewModel(PopupActions actions) => Actions = actions;

    public PopupActions Actions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActions), nameof(IsProcessing), nameof(IsSuggestion), nameof(IsToast))]
    private PopupState _state;

    public bool IsActions => State == PopupState.Actions;
    public bool IsProcessing => State == PopupState.Processing;
    public bool IsSuggestion => State == PopupState.Suggestion;
    public bool IsToast => State == PopupState.Toast;

    // ActionPopup.
    public ObservableCollection<PopupItem> Items { get; } = [];
    public bool HasExtensions => _all.Count > 0;
    /// <summary>A lista aparece com extensões ou, sem nenhuma, quando há um prompt livre para oferecer.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>Estado vazio (nenhuma extensão) só enquanto nada foi digitado.</summary>
    public bool ShowsEmptyState => !HasExtensions && Search.Trim().Length == 0;

    [ObservableProperty]
    private string _search = "";

    partial void OnSearchChanged(string value) => Filter();

    // SuggestionPanel.
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _icon = "wand-sparkles";
    [ObservableProperty] private string? _providerName;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsCode))]
    private bool _isCode;

    /// <summary>Linguagem do realce quando <see cref="IsCode"/> (id TextMate); <c>null</c> sem cores.</summary>
    [ObservableProperty] private string? _codeLanguage;
    [ObservableProperty] private string? _codeFontFamily;
    [ObservableProperty] private double _codeFontSize = 12;
    [ObservableProperty] private string _output = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAct), nameof(IsWaiting))]
    private bool _isStreaming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(CanAct))]
    private string? _error;

    [ObservableProperty] private bool _windowChanged;

    /// <summary>O painel veio da lista: mostra o botão de voltar para escolher outra extensão.</summary>
    [ObservableProperty] private bool _canGoBack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefine))]
    private string _refineText = "";
    [ObservableProperty] private string _toastText = "";
    [ObservableProperty] private string _toastIcon = "info";

    public bool HasError => Error is not null;

    /// <summary>Aplicar/Copiar/Refinar só com um resultado completo.</summary>
    public bool CanAct => !IsStreaming && Error is null && Output.Length > 0;

    /// <summary>Gerando, mas o primeiro pedaço ainda não chegou: mostra o indicador de espera.</summary>
    public bool IsWaiting => IsStreaming && Output.Length == 0;

    public bool CanRefine => CanAct && RefineText.Trim().Length > 0;

    /// <summary>Mostra o resultado no visualizador de código (e não como texto corrido).</summary>
    public bool ShowsCode => IsCode && !IsWaiting && Error is null;

    partial void OnOutputChanged(string value)
    {
        OnPropertyChanged(nameof(CanAct));
        OnPropertyChanged(nameof(CanRefine));
        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(ShowsCode));
    }

    partial void OnIsStreamingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefine));
        OnPropertyChanged(nameof(ShowsCode));
    }

    partial void OnErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(CanRefine));
        OnPropertyChanged(nameof(ShowsCode));
    }

    public PopupItem? Selected => Items.FirstOrDefault(i => i.IsSelected);

    /// <param name="freePromptBase">Extensão cujo provedor e modelo o prompt livre usa; <c>null</c> se nenhuma tem modelo.</param>
    public void ShowActions(IReadOnlyList<Extension> extensions, Extension? freePromptBase = null)
    {
        _freePromptBase = freePromptBase;
        _all = [.. extensions.Select((e, i) => new PopupItem(e, i + 1))];
        Search = "";
        Filter();
        OnPropertyChanged(nameof(HasExtensions));
        OnPropertyChanged(nameof(ShowsEmptyState));
        State = PopupState.Actions;
    }

    /// <param name="codeLanguage">Linguagem já resolvida (escolhida na extensão ou detectada na seleção).</param>
    public void ShowSuggestion(Extension extension, string? providerName, bool canGoBack, string? codeLanguage = null)
    {
        CanGoBack = canGoBack;
        CodeLanguage = codeLanguage;
        Title = extension.Name;
        Icon = extension.Icon;
        ProviderName = providerName;
        IsCode = extension.TreatAsCode;
        CodeFontFamily = extension.CodeFontFamily;
        CodeFontSize = extension.CodeFontSize;
        Output = "";
        Error = null;
        WindowChanged = false;
        RefineText = "";
        State = PopupState.Suggestion;
    }

    /// <summary>Esquece tudo da execução anterior (texto, erro, refinamento, lista) e esconde.</summary>
    public void Clear()
    {
        State = PopupState.Hidden;
        IsStreaming = false;
        Output = "";
        Error = null;
        WindowChanged = false;
        RefineText = "";
        Search = "";
        _all = [];
        Items.Clear();
    }

    public void ShowToast(string text, string icon)
    {
        ToastText = text;
        ToastIcon = icon;
        State = PopupState.Toast;
    }

    /// <summary>Move a seleção (↑/↓), dando a volta nas pontas.</summary>
    public void MoveSelection(int delta)
    {
        if (Items.Count == 0)
            return;
        var index = Selected is { } current ? Items.IndexOf(current) : -1;
        Select(Items[((index + delta) % Items.Count + Items.Count) % Items.Count]);
    }

    public void Select(PopupItem item)
    {
        foreach (var other in Items)
            other.IsSelected = other == item;
        OnPropertyChanged(nameof(Selected));
    }

    /// <summary>Atalho 1–9: pelo número da lista completa, mesmo com filtro.</summary>
    public PopupItem? ByNumber(int number) => _all.FirstOrDefault(i => i.Number == number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [RelayCommand]
    private void Run(PopupItem? item)
    {
        if ((item ?? Selected) is { } chosen)
            _ = Actions.Run(chosen.Extension);
    }

    [RelayCommand]
    private void Refine()
    {
        if (CanRefine && RefineText.Trim() is { } instruction)
        {
            RefineText = "";
            _ = Actions.Refine(instruction);
        }
    }

    [RelayCommand] private void Apply() => _ = CanAct ? Actions.Apply() : Task.CompletedTask;
    [RelayCommand] private void Copy() => _ = CanAct ? Actions.Copy() : Task.CompletedTask;
    [RelayCommand] private void Retry() => _ = Actions.Retry();
    [RelayCommand] private void Close() => Actions.Close();
    [RelayCommand] private void Back() => Actions.Back();
    [RelayCommand] private void OpenSettings() => Actions.OpenSettings(false);
    [RelayCommand] private void CreateExtension() => Actions.OpenSettings(true);

    private void Filter()
    {
        var terms = Search.Trim();
        Items.Clear();
        foreach (var item in _all.Where(i => terms.Length == 0 || i.Name.Contains(terms, StringComparison.CurrentCultureIgnoreCase)))
            Items.Add(item);
        // O texto digitado também vira um pedido avulso, para quando nenhuma extensão serve.
        if (terms.Length > 0)
            Items.Add(new PopupItem(FreePrompt(terms), number: 0, string.Format(CultureInfo.CurrentCulture, Strings.PopupFreePrompt, terms)));
        if (Items.Count > 0)
            Select(Items[0]);
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ShowsEmptyState));
    }

    // Prompt avulso: sempre sugere (nada é colado sem o usuário ver o resultado).
    private Extension FreePrompt(string prompt) => new()
    {
        Name = prompt,
        Icon = "sparkles",
        Prompt = prompt,
        ProviderId = _freePromptBase?.ProviderId,
        Model = _freePromptBase?.Model ?? "",
        Temperature = _freePromptBase?.Temperature,
        Mode = OutputMode.Suggest,
    };
}

/// <summary>O que o popup pede para fazer; implementado pelo <c>FlowController</c>.</summary>
public sealed record PopupActions(
    Func<Extension, Task> Run,
    Func<Task> Apply,
    Func<Task> Copy,
    Func<Task> Retry,
    Func<string, Task> Refine,
    Action Close,
    Action Back,
    Action<bool> OpenSettings);
