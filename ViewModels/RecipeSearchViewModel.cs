using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.ViewModels;

public partial class RecipeSearchViewModel : ObservableObject
{
    private readonly PantryService _pantryService;
    private readonly RecipeApiService _recipeApiService;
    private readonly ILogger<RecipeSearchViewModel> _logger;

    public ObservableCollection<RecipeSearchResult> Recipes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyMessage))]
    private string? emptyMessage;

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasEmptyMessage => !string.IsNullOrWhiteSpace(EmptyMessage);

    public bool HasRecipes => Recipes.Count > 0;

    public RecipeSearchViewModel(
        PantryService pantryService,
        RecipeApiService recipeApiService,
        ILogger<RecipeSearchViewModel> logger)
    {
        _pantryService = pantryService;
        _recipeApiService = recipeApiService;
        _logger = logger;

        Recipes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecipes));
    }

    [RelayCommand]
    private async Task SearchRecipesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        EmptyMessage = null;
        Recipes.Clear();

        try
        {
            var ingredientNames = _pantryService.Load()
                .Where(item => item is not null)
                .Select(item => item.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (ingredientNames.Length == 0)
            {
                EmptyMessage = "Your pantry is empty. Add ingredients before searching for recipes.";
                return;
            }

            var results = await _recipeApiService.SearchByIngredientNamesAsync(
                ingredientNames);

            foreach (var recipe in results)
            {
                Recipes.Add(recipe);
            }

            if (results.Count == 0)
            {
                EmptyMessage = "No recipes matched the ingredients saved in your pantry.";
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
                IOException or
                UnauthorizedAccessException or
                JsonException or
                TaskCanceledException or
                InvalidOperationException)
        {
            _logger.LogError(ex, "Recipe search failed.");
            ErrorMessage = $"Could not search for recipes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
