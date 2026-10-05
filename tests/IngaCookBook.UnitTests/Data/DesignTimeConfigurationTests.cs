using System.Diagnostics.CodeAnalysis;
using IngaCookBook.Data;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace IngaCookBook.UnitTests.Data;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class DesignTimeConfigurationTests
{
    [Fact]
    public void MissingRuntimeConnectionsRemainMissing()
    {
        using var configuration = new ConfigurationManager();
        DesignTimeConfiguration.Configure(configuration, isDesignTime: false);
        configuration.GetConnectionString("ingacookbookdb").ShouldBeNull();
        configuration.GetConnectionString("recipephotos").ShouldBeNull();
    }

    [Fact]
    public void ModelBuildUsesNonRoutableDefaultsWithoutAzureOrDatabase()
    {
        using var configuration = new ConfigurationManager();
        DesignTimeConfiguration.Configure(configuration, isDesignTime: true);
        configuration.GetConnectionString("ingacookbookdb").ShouldBe("Host=design-time.invalid;Database=ingacookbook;Timeout=1");
        configuration.GetConnectionString("recipephotos").ShouldBe("https://design-time.invalid");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitConnectionsArePreserved(bool isDesignTime)
    {
        using var configuration = new ConfigurationManager
        {
            ["ConnectionStrings:ingacookbookdb"] = "Host=provided.invalid;Database=supplied",
            ["ConnectionStrings:recipephotos"] = "https://provided.invalid",
        };
        DesignTimeConfiguration.Configure(configuration, isDesignTime);
        configuration.GetConnectionString("ingacookbookdb").ShouldBe("Host=provided.invalid;Database=supplied");
        configuration.GetConnectionString("recipephotos").ShouldBe("https://provided.invalid");
    }
}
