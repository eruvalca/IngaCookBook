using System.Diagnostics.CodeAnalysis;
using Bunit;
using IngaCookBook.SharedKernel.Notebook;
using IngaCookBook.UI.Features.Notebook;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace IngaCookBook.ComponentTests.Features.Notebook;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NotebookCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationDeadlineCancelsWorkAndReportsTheAppropriateRecovery(bool saving)
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton(Substitute.For<INotebookService>());
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        var component = context.Render<DeadlineProbe>(p => p.Add(c => c.Saving, saving));
        await component.Find("button").ClickAsync();
        component.Instance.ObservedToken.IsCancellationRequested.ShouldBeTrue();
        component.Find("p").TextContent.ShouldContain(saving ? "save may have completed" : "Loading took too long");
        component.Find("button").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task DisposalIsIdempotentAndCancelsBeforeCleanup()
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton(Substitute.For<INotebookService>());
        var component = context.Render<DeadlineProbe>();
        var instance = component.Instance;
        await context.DisposeAsync();
        await instance.DisposeAsync();
        instance.CleanupCount.ShouldBe(1);
        instance.CleanupSawCancellation.ShouldBeTrue();
    }

    [Fact]
    public async Task OldCompletionCannotUnlockOrPublishOverAStillPendingLoad()
    {
        await using var context = new BunitContext();
        context.Services.AddSingleton(Substitute.For<INotebookService>());
        context.Renderer.SetRendererInfo(new RendererInfo("Server", true));
        var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var component = context.Render<DeadlineProbe>(p => p.Add(c => c.Load, _ => first.Task));
        component.Render(p => p.Add(c => c.Load, _ => second.Task));
        var rendered = component.RenderCount;
        first.SetResult("Old result");
        await component.WaitForAssertionAsync(() => component.RenderCount.ShouldBeGreaterThan(rendered));
        component.Find("button").HasAttribute("disabled").ShouldBeTrue();
        component.Find("p").TextContent.ShouldBeEmpty();
        second.SetResult("Current result");
        await component.WaitForAssertionAsync(() =>
        {
            component.Find("p").TextContent.ShouldBe("Current result");
            component.Find("button").HasAttribute("disabled").ShouldBeFalse();
        });
    }

    // A small rendered consumer tests the intentional base-class contract. Real editor,
    // navigation guard, service and upload behavior is covered separately.
    private sealed class DeadlineProbe : NotebookPage
    {
        private string? _value;
        [Parameter] public bool Saving { get; set; }
        [Parameter] public Func<CancellationToken, Task<string>>? Load { get; set; }
        public CancellationToken ObservedToken { get; private set; }
        public int CleanupCount { get; private set; }
        public bool CleanupSawCancellation { get; private set; }
        protected override TimeSpan LoadTimeout => Load is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(30);
        protected override TimeSpan SaveTimeout => TimeSpan.FromSeconds(1);

        protected override Task OnParametersSetAsync() => Load is { } load
            ? LoadAsync(async ct => _value = await ReceiveAsync(load(ct), ct))
            : Task.CompletedTask;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, ExecuteAsync));
            builder.AddAttribute(2, "disabled", Busy);
            builder.AddContent(3, "Start");
            builder.CloseElement();
            builder.OpenElement(4, "p");
            builder.AddContent(5, Error);
            builder.AddContent(6, _value);
            builder.CloseElement();
        }

        private Task ExecuteAsync() => Saving ? RunAsync(WaitAsync) : LoadAsync(WaitAsync);
        private async Task WaitAsync(CancellationToken cancellationToken)
        {
            ObservedToken = cancellationToken;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        protected override async ValueTask DisposeCoreAsync()
        {
            await base.DisposeCoreAsync();
            CleanupCount++;
            CleanupSawCancellation = LifetimeToken.IsCancellationRequested;
        }
    }
}
