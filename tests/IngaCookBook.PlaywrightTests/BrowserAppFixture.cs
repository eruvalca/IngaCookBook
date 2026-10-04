using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using IngaCookBook.Testing;
using Xunit;

[assembly: AssemblyFixture(typeof(IngaCookBook.PlaywrightTests.BrowserAppFixture))]

namespace IngaCookBook.PlaywrightTests;

/// <summary>Owns one disposable application while concurrent tests own their browsers and accounts.</summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public fixture types for constructor injection.")]
public sealed class BrowserAppFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? _builder;
    private DistributedApplication? _app;
    public Uri Endpoint { get; private set; } = default!;
    public Uri InboxEndpoint { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        startup.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            _builder = await TestAppHost.CreateAsync(startup.Token);
            _app = await _builder.BuildAsync(startup.Token);
            await TestAppHost.StartAsync(_app, message => TestContext.Current.SendDiagnosticMessage(message), startup.Token);
            Endpoint = _app.GetEndpoint("ingacookbook", "https");
            InboxEndpoint = _app.GetEndpoint("mailpit", "http");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_app is not null)
            {
                await _app.DisposeAsync();
                _app = null;
            }
        }
        finally
        {
            if (_builder is not null)
            {
                await _builder.DisposeAsync();
                _builder = null;
            }
        }
        GC.SuppressFinalize(this);
    }
}
