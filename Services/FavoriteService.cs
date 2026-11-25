using System.Text.Json;
using PantryPal.Models;

namespace PantryPal.Services;

/// Persists complete favorite recipes in the app's local data directory.
public sealed class FavoriteService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    private readonly string _favoritesPath =
        Path.Combine(FileSystem.AppDataDirectory, "favorites.json");
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public async Task<List<RecipeSearchResult>> GetFavoritesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            List<RecipeSearchResult> favorites = await ReadFavoritesAsync();
            foreach (RecipeSearchResult recipe in favorites)
            {
                recipe.IsFavorite = true;
            }

            return favorites;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AddFavoriteAsync(RecipeSearchResult recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ValidateRecipeId(recipe.RecipeID);

        await _fileLock.WaitAsync();
        try
        {
            List<RecipeSearchResult> favorites = await ReadFavoritesAsync();
            int existingIndex = favorites.FindIndex(
                favorite => favorite.RecipeID == recipe.RecipeID);
            if (existingIndex >= 0)
            {
                favorites[existingIndex] = recipe;
            }
            else
            {
                favorites.Add(recipe);
            }

            await WriteFavoritesAsync(favorites);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RemoveFavoriteAsync(int recipeId)
    {
        ValidateRecipeId(recipeId);

        await _fileLock.WaitAsync();
        try
        {
            List<RecipeSearchResult> favorites = await ReadFavoritesAsync();
            favorites.RemoveAll(recipe => recipe.RecipeID == recipeId);
            await WriteFavoritesAsync(favorites);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<RecipeSearchResult>> ReadFavoritesAsync()
    {
        if (!File.Exists(_favoritesPath))
        {
            return [];
        }

        string json = await File.ReadAllTextAsync(_favoritesPath);
        try
        {
            return JsonSerializer.Deserialize<List<RecipeSearchResult>>(
                json,
                JsonOptions) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The saved favorites file contains invalid recipe data.",
                exception);
        }
    }

    private async Task WriteFavoritesAsync(List<RecipeSearchResult> favorites)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_favoritesPath)!);
        string json = JsonSerializer.Serialize(favorites, JsonOptions);
        await File.WriteAllTextAsync(_favoritesPath, json);
    }

    private static void ValidateRecipeId(int recipeId)
    {
        if (recipeId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recipeId),
                "A favorite recipe must have a positive recipe ID.");
        }
    }
}
