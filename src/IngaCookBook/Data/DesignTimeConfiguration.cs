namespace IngaCookBook.Data;

internal static class DesignTimeConfiguration
{
    internal static void Configure(IConfiguration configuration, bool isDesignTime)
    {
        if (!isDesignTime)
        {
            return;
        }

        // Building a migration bundle needs the real startup model, not live infrastructure.
        // Non-routable defaults cannot accidentally update a developer/production database.
        // Actual EF update commands retain the connection strings supplied by Aspire.
        configuration["ConnectionStrings:ingacookbookdb"] ??= "Host=design-time.invalid;Database=ingacookbook;Timeout=1";
        configuration["ConnectionStrings:recipephotos"] ??= "https://design-time.invalid";
    }
}
