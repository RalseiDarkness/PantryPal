
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

    public async Task<List<Recipe>> GetAllRecipesAsync()
    {
        var recipes = new List<Recipe>();

        await using var connection =
            await _sqlFactory.CreateConnectionAsync();

        await connection.OpenAsync();

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

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
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
        IReadOnlyCollection<string> ingredientNames)
    {
        var recipes = new List<RecipeSearchResult>();

        await using var connection =
            await _sqlFactory.CreateConnectionAsync();

        await connection.OpenAsync();

        const string sql = """
            WITH SelectedIngredients AS (
                SELECT selected.IngredientID
                FROM OPENJSON(@IngredientIdsJson)
                WITH (IngredientID int '$') AS selected
                INNER JOIN Ingredients AS i
                    ON i.IngredientID = selected.IngredientID

                UNION

                SELECT i.IngredientID
                FROM OPENJSON(@IngredientNamesJson)
                WITH (IngredientName nvarchar(4000) '$') AS selected
                INNER JOIN Ingredients AS i
                    ON i.IngredientName = LTRIM(RTRIM(selected.IngredientName))
            ),
            MatchingRecipes AS (
                SELECT
                    ri.RecipeID,
                    COUNT(DISTINCT ri.IngredientID) AS MatchingIngredientCount
                FROM RecipeIngredients AS ri
                INNER JOIN SelectedIngredients AS si
                    ON si.IngredientID = ri.IngredientID
                GROUP BY ri.RecipeID
            ),
            RecipeIngredientCounts AS (
                SELECT
                    mr.RecipeID,
                    mr.MatchingIngredientCount,
                    COUNT(DISTINCT ri.IngredientID) AS TotalIngredientCount
                FROM MatchingRecipes AS mr
                INNER JOIN RecipeIngredients AS ri
                    ON ri.RecipeID = mr.RecipeID
                GROUP BY mr.RecipeID, mr.MatchingIngredientCount
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
                (
                    SELECT i.IngredientName AS [Name]
                    FROM RecipeIngredients AS ri
                    INNER JOIN Ingredients AS i
                        ON i.IngredientID = ri.IngredientID
                    INNER JOIN SelectedIngredients AS si
                        ON si.IngredientID = ri.IngredientID
                    WHERE ri.RecipeID = r.RecipeID
                    ORDER BY i.IngredientName
                    FOR JSON PATH
                ) AS MatchingIngredientsJson,
                (
                    SELECT i.IngredientName AS [Name]
                    FROM RecipeIngredients AS ri
                    INNER JOIN Ingredients AS i
                        ON i.IngredientID = ri.IngredientID
                    WHERE ri.RecipeID = r.RecipeID
                      AND NOT EXISTS (
                          SELECT 1
                          FROM SelectedIngredients AS si
                          WHERE si.IngredientID = ri.IngredientID
                      )
                    ORDER BY i.IngredientName
                    FOR JSON PATH
                ) AS MissingIngredientsJson
            FROM RecipeIngredientCounts AS ric
            INNER JOIN Recipes AS r
                ON r.RecipeID = ric.RecipeID
            ORDER BY
                ric.MatchingIngredientCount DESC,
                ric.TotalIngredientCount - ric.MatchingIngredientCount ASC,
                r.DateAdded DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@IngredientIdsJson", SqlDbType.NVarChar, -1)
            .Value = JsonSerializer.Serialize(ingredientIds);
        command.Parameters.Add("@IngredientNamesJson", SqlDbType.NVarChar, -1)
            .Value = JsonSerializer.Serialize(ingredientNames);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
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
                MatchingIngredients = DeserializeIngredientNames(reader.GetString(10)),
                MissingIngredients = DeserializeIngredientNames(reader.GetString(11))
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

    private sealed class IngredientNameJson
    {
        public string Name { get; set; } = string.Empty;
    }
}
