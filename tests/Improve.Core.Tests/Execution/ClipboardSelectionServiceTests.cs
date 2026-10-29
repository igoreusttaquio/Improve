using Improve.Core.Execution;
using Improve.Core.Tests.Fakes;

namespace Improve.Core.Tests.Execution;

public class ClipboardSelectionServiceTests
{
    private static readonly SelectionTimings Fast = new(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(1));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Capture_returns_selection_and_restores_the_user_clipboard()
    {
        var desktop = new FakeDesktop { Text = "copiado antes", Selection = "texto selecionado" };
        var service = new ClipboardSelectionService(desktop, desktop, Fast);

        var capture = await service.CaptureAsync(Ct);

        Assert.Equal("texto selecionado", capture.Text);
        Assert.Equal("copiado antes", capture.PreviousClipboard);
        Assert.Equal("copiado antes", desktop.Text);
    }

    [Fact]
    public async Task Capture_without_selection_returns_null_and_restores()
    {
        var desktop = new FakeDesktop { Text = "copiado antes", Selection = null };
        var service = new ClipboardSelectionService(desktop, desktop, Fast);

        var capture = await service.CaptureAsync(Ct);

        Assert.Null(capture.Text);
        Assert.Equal("copiado antes", desktop.Text);
    }

    [Fact]
    public async Task Paste_puts_result_pastes_and_restores_the_previous_clipboard()
    {
        var desktop = new FakeDesktop { Text = "do usuário" };
        var service = new ClipboardSelectionService(desktop, desktop, Fast);

        await service.PasteAsync("resultado", Ct);

        Assert.Equal(["resultado"], desktop.Pasted);
        Assert.Equal("do usuário", desktop.Text);
    }

    [Fact]
    public async Task Paste_does_not_restore_when_the_user_copied_something_meanwhile()
    {
        var desktop = new FakeDesktop { Text = "antigo" };
        desktop.AfterPaste = () => desktop.Text = "novo do usuário";
        var service = new ClipboardSelectionService(desktop, desktop, Fast);

        await service.PasteAsync("resultado", Ct);

        Assert.Equal("novo do usuário", desktop.Text);
        Assert.DoesNotContain("restore", desktop.Log);
    }

    [Fact]
    public async Task Copy_leaves_the_result_in_the_clipboard()
    {
        var desktop = new FakeDesktop { Text = "antigo" };
        var service = new ClipboardSelectionService(desktop, desktop, Fast);

        await service.CopyAsync("resultado", Ct);

        Assert.Equal("resultado", desktop.Text);
    }
}
