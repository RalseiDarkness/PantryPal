using Azure.Core;
using Microsoft.Extensions.Logging.Abstractions;
using PantryPal.Api.Data;
using PantryPal.Api.Services;

namespace PantryPal.Tests;

public class RecipeServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetMatchingRecipesAsync_RejectsNonPositiveMaximumResults(
        int maximumResults)
    {
        var service = CreateRecipeService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetMatchingRecipesAsync(
                [],
                [],
                maximumResults: maximumResults));
    }

    [Fact]
    public void NormalizeIngredientIds_RemovesDuplicatesAndPreservesOrder()
    {
        var normalized = RecipeService.NormalizeIngredientIds([5, 5, 8, 5]);

        Assert.Equal([5, 8], normalized);
    }

    private static RecipeService CreateRecipeService()
    {
        var connectionFactory = new SqlConnectionFactory(
            new UnusedTokenCredential(),
            NullLogger<SqlConnectionFactory>.Instance);
        return new RecipeService(connectionFactory);
    }

    private sealed class UnusedTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The SQL connection must not be opened by this test.");
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "The SQL connection must not be opened by this test.");
        }
    }
}
