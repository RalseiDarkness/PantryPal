
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using PantryPal.Api.Data;
using PantryPal.Api.Models;

namespace PantryPal.Api.Services;

public class RecipeService
{
    private readonly SqlConnectionFactory _sqlFactory;

    public RecipeService(SqlConnectionFactory sqlFactory)
    {
        _sqlFactory = sqlFactory;
    }

    public async Task<List<IngredientSuggestion>> SearchIngredientsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var suggestions = new List<IngredientSuggestion>();
        await using var connection =
            await _sqlFactory.CreateConnectionAsync(cancellationToken);

        var escapedQuery = EscapeLikePattern(query.Trim());
        const string sql = """
            WITH MatchingNames AS (
                SELECT
                    IngredientName COLLATE Latin1_General_100_BIN2 AS NameKey,
                    DATALENGTH(IngredientName) AS NameByteLength,
                    MIN(CASE
                        WHEN IngredientName COLLATE Latin1_General_100_CI_AS =
                                @ExactQuery COLLATE Latin1_General_100_CI_AS
                            AND DATALENGTH(IngredientName) = DATALENGTH(@ExactQuery)
                            THEN 0
                        WHEN IngredientName COLLATE Latin1_General_100_CI_AS
                            LIKE @PrefixPattern ESCAPE '\' THEN 1
                        WHEN IngredientName COLLATE Latin1_General_100_CI_AS
                            LIKE @WordPrefixPattern ESCAPE '\' THEN 2
                        ELSE 3
                    END) AS MatchRank,
                    MIN(IngredientID) AS DisplayIngredientID
                FROM Ingredients
                WHERE IngredientName COLLATE Latin1_General_100_CI_AS
                    LIKE @ContainsPattern ESCAPE '\'
                GROUP BY
                    IngredientName COLLATE Latin1_General_100_BIN2,
                    DATALENGTH(IngredientName)
            ),
            TopNames AS (
                SELECT TOP (10)
                    NameKey,
                    NameByteLength,
                    MatchRank,
                    DisplayIngredientID
                FROM MatchingNames
                ORDER BY MatchRank, NameKey, NameByteLength, DisplayIngredientID
            ),
            SelectedNames AS (
                SELECT
                    ROW_NUMBER() OVER (
                        ORDER BY
                            names.MatchRank,
                            names.NameKey,
                            names.NameByteLength,
                            names.DisplayIngredientID
                    ) AS NameOrdinal,
                    displayName.IngredientName,
                    names.NameKey,
                    names.NameByteLength
                FROM TopNames AS names
                INNER JOIN Ingredients AS displayName
                    ON displayName.IngredientID = names.DisplayIngredientID
            )
            SELECT
                selected.NameOrdinal,
                selected.IngredientName,
                ingredient.IngredientID
            FROM SelectedNames AS selected
            INNER JOIN Ingredients AS ingredient
                ON ingredient.IngredientName COLLATE Latin1_General_100_BIN2
                    = selected.NameKey
                    AND DATALENGTH(ingredient.IngredientName) =
                        selected.NameByteLength
            ORDER BY selected.NameOrdinal, ingredient.IngredientID;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ExactQuery", SqlDbType.NVarChar, 100)
            .Value = query.Trim();
        command.Parameters.Add("@PrefixPattern", SqlDbType.NVarChar, 202)
            .Value = $"{escapedQuery}%";
        command.Parameters.Add("@WordPrefixPattern", SqlDbType.NVarChar, 214)
            .Value = $"%[^a-zA-Z0-9]{escapedQuery}%";
        command.Parameters.Add("@ContainsPattern", SqlDbType.NVarChar, 402)
            .Value = $"%{escapedQuery}%";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        long currentNameOrdinal = 0;
        string? currentName = null;
        List<int>? currentIngredientIds = null;

        while (await reader.ReadAsync(cancellationToken))
        {
            var nameOrdinal = reader.GetInt64(0);
            if (nameOrdinal != currentNameOrdinal)
            {
                if (currentName is not null && currentIngredientIds is not null)
                {
                    suggestions.Add(new IngredientSuggestion(
                        currentName,
                        currentIngredientIds));
                }

                currentNameOrdinal = nameOrdinal;
                currentName = reader.GetString(1);
                currentIngredientIds = [];
            }

            currentIngredientIds!.Add(reader.GetInt32(2));
        }

        if (currentName is not null && currentIngredientIds is not null)
        {
            suggestions.Add(new IngredientSuggestion(
                currentName,
                currentIngredientIds));
        }

        return suggestions;
    }

    public async Task<List<string>> GetIngredientNamesAsync(
        IReadOnlyCollection<int> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        await using var connection =
            await _sqlFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            SELECT i.IngredientName
            FROM OPENJSON(@IngredientIdsJson)
            WITH (IngredientID int '$') AS selected
            INNER JOIN Ingredients AS i
                ON i.IngredientID = selected.IngredientID
            ORDER BY i.IngredientName;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IngredientIdsJson", SqlDbType.NVarChar, -1)
            .Value = JsonSerializer.Serialize(ingredientIds);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public async Task<List<Recipe>> GetAllRecipesAsync(
        CancellationToken cancellationToken = default)
    {
        var recipes = new List<Recipe>();

        await using var connection =
            await _sqlFactory.CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                RecipeID,
                Title,
                ImageURL,
                ReadyInMinutes,
                Servings,
                SourceUrl,
                Instructions,
                DateAdded
            FROM Recipes
            ORDER BY DateAdded DESC;";

        await using var command = new SqlCommand(sql, connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            recipes.Add(new Recipe
            {
                RecipeID = reader.GetInt32(0),
                Title = reader.GetString(1),
                ImageURL = reader.IsDBNull(2) ? null : reader.GetString(2),
                ReadyInMinutes = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Servings = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                SourceUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                Instructions = reader.IsDBNull(6) ? null : reader.GetString(6),
                DateAdded = reader.GetDateTime(7)
            });
        }

        return recipes;
    }

    public async Task<List<RecipeSearchResult>> GetMatchingRecipesAsync(
        IReadOnlyCollection<int> ingredientIds,
        IReadOnlyCollection<string> ingredientNames,
        CancellationToken cancellationToken = default,
        int? maximumResults = null)
    {
        var recipes = new List<RecipeSearchResult>();

        await using var connection =
            await _sqlFactory.CreateConnectionAsync(cancellationToken);

        const string sql = """
            WITH SelectedIngredientNameRows AS (
                SELECT i.IngredientName
                FROM OPENJSON(@IngredientIdsJson)
                WITH (IngredientID int '$') AS selected
                INNER JOIN Ingredients AS i
                    ON i.IngredientID = selected.IngredientID

                UNION ALL

                SELECT i.IngredientName
                FROM OPENJSON(@IngredientNamesJson)
                WITH (IngredientName nvarchar(4000) '$') AS selected
                INNER JOIN Ingredients AS i
                    ON i.IngredientName = LTRIM(RTRIM(selected.IngredientName))
            ),
            SelectedIngredients AS (
                SELECT
                    IngredientName COLLATE Latin1_General_100_BIN2 AS IngredientName,
                    DATALENGTH(IngredientName) AS IngredientNameByteLength
                FROM SelectedIngredientNameRows
                GROUP BY
                    IngredientName COLLATE Latin1_General_100_BIN2,
                    DATALENGTH(IngredientName)
            ),
            RecipeIngredientsByName AS (
                SELECT DISTINCT
                    ri.RecipeID,
                    i.IngredientName COLLATE Latin1_General_100_BIN2 AS IngredientName,
                    DATALENGTH(i.IngredientName) AS IngredientNameByteLength
                FROM RecipeIngredients AS ri
                INNER JOIN Ingredients AS i
                    ON i.IngredientID = ri.IngredientID
            ),
            MatchingRecipes AS (
                SELECT
                    recipeIngredient.RecipeID,
                    COUNT(*) AS MatchingIngredientCount
                FROM RecipeIngredientsByName AS recipeIngredient
                INNER JOIN SelectedIngredients AS selected
                    ON selected.IngredientName = recipeIngredient.IngredientName
                    AND selected.IngredientNameByteLength =
                        recipeIngredient.IngredientNameByteLength
                GROUP BY recipeIngredient.RecipeID
            ),
            RecipeIngredientCounts AS (
                SELECT
                    mr.RecipeID,
                    mr.MatchingIngredientCount,
                    COUNT(*) AS TotalIngredientCount
                FROM MatchingRecipes AS mr
                INNER JOIN RecipeIngredientsByName AS recipeIngredient
                    ON recipeIngredient.RecipeID = mr.RecipeID
                GROUP BY mr.RecipeID, mr.MatchingIngredientCount
            ),
            SelectedIngredientCount AS (
                SELECT COUNT(*) AS Count
                FROM SelectedIngredients
            )
            SELECT
                r.RecipeID,
                r.Title,
                r.ImageURL,
                r.ReadyInMinutes,
                r.Servings,
                r.SourceUrl,
                r.Instructions,
                r.DateAdded,
                ric.MatchingIngredientCount,
                ric.TotalIngredientCount - ric.MatchingIngredientCount
                    AS MissingIngredientCount,
                CAST(
                    ric.MatchingIngredientCount * 100.0 /
                    NULLIF(sic.Count, 0)
                    AS decimal(5, 2)
                ) AS MatchPercentage,
                (
                    SELECT recipeIngredient.IngredientName AS [Name]
                    FROM RecipeIngredientsByName AS recipeIngredient
                    INNER JOIN SelectedIngredients AS selected
                        ON selected.IngredientName = recipeIngredient.IngredientName
                        AND selected.IngredientNameByteLength =
                            recipeIngredient.IngredientNameByteLength
                    WHERE recipeIngredient.RecipeID = r.RecipeID
                    ORDER BY recipeIngredient.IngredientName
                    FOR JSON PATH
                ) AS MatchingIngredientsJson,
                (
                    SELECT recipeIngredient.IngredientName AS [Name]
                    FROM RecipeIngredientsByName AS recipeIngredient
                    WHERE recipeIngredient.RecipeID = r.RecipeID
                      AND NOT EXISTS (
                          SELECT 1
                          FROM SelectedIngredients AS selected
                          WHERE selected.IngredientName =
                              recipeIngredient.IngredientName
                            AND selected.IngredientNameByteLength =
                                recipeIngredient.IngredientNameByteLength
                      )
                    ORDER BY recipeIngredient.IngredientName
                    FOR JSON PATH
                ) AS MissingIngredientsJson
            FROM RecipeIngredientCounts AS ric
            CROSS JOIN SelectedIngredientCount AS sic
            INNER JOIN Recipes AS r
                ON r.RecipeID = ric.RecipeID
            ORDER BY
                ric.MatchingIngredientCount DESC,
                MatchPercentage DESC,
                ric.TotalIngredientCount - ric.MatchingIngredientCount ASC,
                r.DateAdded DESC
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IngredientIdsJson", SqlDbType.NVarChar, -1)
            .Value = JsonSerializer.Serialize(ingredientIds);
        command.Parameters.Add("@IngredientNamesJson", SqlDbType.NVarChar, -1)
            .Value = JsonSerializer.Serialize(ingredientNames);

        if (maximumResults is not null)
        {
            command.CommandText += "\nOFFSET 0 ROWS FETCH NEXT @MaximumResults ROWS ONLY;";
            command.Parameters.Add("@MaximumResults", SqlDbType.Int)
                .Value = maximumResults.Value;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            recipes.Add(new RecipeSearchResult
            {
                RecipeID = reader.GetInt32(0),
                Title = reader.GetString(1),
                ImageURL = reader.IsDBNull(2) ? null : reader.GetString(2),
                ReadyInMinutes = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Servings = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                SourceUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                Instructions = reader.IsDBNull(6) ? null : reader.GetString(6),
                DateAdded = reader.GetDateTime(7),
                MatchingIngredientCount = reader.GetInt32(8),
                MissingIngredientCount = reader.GetInt32(9),
                MatchPercentage = reader.GetDecimal(10),
                MatchingIngredients = DeserializeIngredientNames(
                    reader.IsDBNull(11) ? "[]" : reader.GetString(11)),
                MissingIngredients = DeserializeIngredientNames(
                    reader.IsDBNull(12) ? "[]" : reader.GetString(12))
            });
        }

        return recipes;
    }

    private static List<string> DeserializeIngredientNames(string json)
    {
        return JsonSerializer.Deserialize<List<IngredientNameJson>>(json)?
            .Select(ingredient => ingredient.Name)
            .ToList() ?? [];
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
    }

    private sealed class IngredientNameJson
    {
        public string Name { get; set; } = string.Empty;
    }
}
