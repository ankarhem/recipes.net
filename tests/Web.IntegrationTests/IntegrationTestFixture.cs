using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Temporalio.Client;
using Temporalio.Testing;
using Xunit;

namespace Web.IntegrationTests;

public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private WorkflowEnvironment? _environment;

    public async Task InitializeAsync()
    {
        _environment = await WorkflowEnvironment.StartLocalAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var target = ((TemporalClient)_environment!.Client).Connection.Options.TargetHost!;
        builder.UseSetting("Temporal:Target", target);
        builder.UseSetting("OpenAi:ApiKey", "test-key-for-integration-tests");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_environment != null)
        {
            await _environment.DisposeAsync();
        }
    }
}
