using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.ViewModels;

public partial class RecipeSearchViewModel : ObservableObject
{
    private readonly RecipeApiService _recipeApiService;
    private readonly ILogger<RecipeSearchViewModel> _logger;
    private readonly object _suggestionSearchLock = new();
    private readonly AutocompleteSearchGeneration _suggestionSearchGeneration = new();
    private CancellationTokenSource? _suggestionSearchCancellation;

    public ObservableCollection<IngredientSuggestion> Suggestions { get; } = [];

    public ObservableCollection<IngredientSuggestion> SelectedIngredients { get; } = [];

    public ObservableCollection<RecipeSearchResult> Recipes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSuggestions))]
    private string ingredientQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(CanSearchRecipes))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuggestionError))]
    [NotifyPropertyChangedFor(nameof(HasNoSuggestions))]
    private string? suggestionErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyMessage))]
    private string? emptyMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiStatusMessage))]
    private string? aiStatusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecommendationMessage))]
    private string? recommendationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSuggestions))]
    private bool isSearchingIngredients;

    public bool IsNotBusy => !IsBusy;

    public bool CanSearchRecipes => !IsBusy && SelectedIngredients.Count > 0;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool HasNoSuggestions =>
        IngredientQuery.Trim().Length >= 2 &&
        !IsSearchingIngredients &&
        Suggestions.Count == 0 &&
        !HasSuggestionError;

    public bool HasSuggestionError =>
        !string.IsNullOrWhiteSpace(SuggestionErrorMessage);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasEmptyMessage => !string.IsNullOrWhiteSpace(EmptyMessage);

    public bool HasAiStatusMessage => !string.IsNullOrWhiteSpace(AiStatusMessage);

    public bool HasRecommendationMessage =>
        !string.IsNullOrWhiteSpace(RecommendationMessage);

    public bool HasRecipes => Recipes.Count > 0;

    public bool HasNoSelectedIngredients => SelectedIngredients.Count == 0;

    public RecipeSearchViewModel(
        RecipeApiService recipeApiService,
        ILogger<RecipeSearchViewModel> logger)
    {
        _recipeApiService = recipeApiService;
        _logger = logger;

        Suggestions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSuggestions));
            OnPropertyChanged(nameof(HasNoSuggestions));
        };
        SelectedIngredients.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanSearchRecipes));
            OnPropertyChanged(nameof(HasNoSelectedIngredients));
        };
        Recipes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecipes));
    }

partial void OnIngredientQueryChanged(string value)
{
        var version = _suggestionSearchGeneration.Begin();
        var query = value.Trim();
        CancellationTokenSource? cancellation = null;

        lock (_suggestionSearchLock)
        {
            _suggestionSearchCancellation?.Cancel();

            if (query.Length >= 2)
            {
                cancellation = new CancellationTokenSource();
                _suggestionSearchCancellation = cancellation;
            }
            else
            {
                _suggestionSearchCancellation = null;
            }
        }

        Suggestions.Clear();
        SuggestionErrorMessage = null;

        if (query.Length < 2)
        {
            IsSearchingIngredients = false;
            return;
        }

        IsSearchingIngredients = true;

        _ = LoadSuggestionsAsync(
            query,
            cancellation!,
            cancellation!.Token,
            version);
    }


    private async Task LoadSuggestionsAsync(
        string query,
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken,
        int version)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);
            var suggestions = await _recipeApiService.SearchIngredientsAsync(
                query,
                cancellationToken);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_suggestionSearchGeneration.IsCurrent(version) ||
                    cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                Suggestions.Clear();
                foreach (var suggestion in suggestions.Where(
                             suggestion => !SelectedIngredients.Any(selected =>
                                 IngredientSuggestion.HasSameNormalizedName(
                                     selected.IngredientName,
                                     suggestion.IngredientName))))
                {
                    Suggestions.Add(suggestion);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (
            ex is HttpRequestException or
                JsonException or
                InvalidOperationException)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_suggestionSearchGeneration.IsCurrent(version) ||
                    cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogError(ex, "Ingredient autocomplete failed.");
                SuggestionErrorMessage =
                    $"Could not load ingredient suggestions: {ex.Message}";
            });
        }
        finally
        {
            bool wasActive;

            lock (_suggestionSearchLock)
            {
                wasActive = ReferenceEquals(_suggestionSearchCancellation, cancellation);

                if (wasActive)
                {
                    _suggestionSearchCancellation = null;
                }

                cancellation.Dispose();
            }

            if (wasActive)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (_suggestionSearchGeneration.IsCurrent(version))
                    {
                        IsSearchingIngredients = false;
                    }
                });
            }
        }
    }

    public void SelectIngredient(IngredientSuggestion ingredient)
    {
        if (!IngredientSuggestion.TryAddOrMergeSelection(
                SelectedIngredients,
                ingredient))
        {
            ErrorMessage = "This ingredient suggestion has no database IDs. Please search again.";
            return;
        }

        IngredientQuery = string.Empty;
        SuggestionErrorMessage = null;
        ErrorMessage = null;
    }

    public void RemoveIngredient(IngredientSuggestion ingredient)
    {
        SelectedIngredients.Remove(ingredient);
    }

    [RelayCommand]
    private async Task SearchRecipesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedIngredients.Count == 0)
        {
            EmptyMessage = "Select at least one ingredient before searching.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        EmptyMessage = null;
        AiStatusMessage = null;
        RecommendationMessage = null;
        Recipes.Clear();

        try
        {
            var recipes = await _recipeApiService.SearchByIngredientIdsAsync(
                IngredientSuggestion.GetDistinctIngredientIds(SelectedIngredients));

            foreach (var recipe in recipes)
            {
                Recipes.Add(recipe);
            }

            if (recipes.Count == 0)
            {
                EmptyMessage = "No recipes matched the selected ingredients.";
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
            _logger.LogError(ex, "Database recipe search failed.");
            ErrorMessage = $"Could not find recipes: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleInstructions(RecipeSearchResult recipe)
    {
        recipe.IsInstructionsVisible = !recipe.IsInstructionsVisible;
    }

    [RelayCommand]
    private async Task OpenRecipeSourceAsync(RecipeSearchResult recipe)
    {
        if (!Uri.TryCreate(recipe.SourceUrl, UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp &&
             sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "This recipe does not have a valid source link.";
            return;
        }

        try
        {
            await Launcher.OpenAsync(sourceUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open recipe source {SourceUrl}.", recipe.SourceUrl);
            ErrorMessage = "Could not open the recipe source link.";
        }
    }
}
