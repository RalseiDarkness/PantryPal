using System.Text.Json;
using PantryPal.Models;
using PantryPal.Services;

namespace PantryPal.PageViews;

/// Displays SQL recipe details immediately, then loads pantry-specific AI analysis
/// when the page first appears.
public partial class RecipeDetailView : ContentPage
{
    // the recipe this page shows
    private RecipeSearchResult _recipe;
    private readonly RecipeApiService _apiService;
    private readonly List<string> _pantryIngredients;
    private bool _analysisRequested;

    // the recipes page passes in the recipe that was tapped
    /// Receives the selected result and a snapshot of the pantry used to find it.
    public RecipeDetailView(
        RecipeSearchResult recipe,
        RecipeApiService apiService,
        List<string> pantryIngredients)
    {
        InitializeComponent();
        _recipe = recipe;
        _apiService = apiService;
        _pantryIngredients = pantryIngredients;
        BindingContext = recipe; // every {Binding} on this page reads from this recipe
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Avoid regenerating analysis if this page reappears after opening the
        // cooking guide; the API also caches successful analyses by pantry context.
        if (_analysisRequested)
        {
            return;
        }

        _analysisRequested = true;
        if (_pantryIngredients.Count == 0)
        {
            // Saved recipe data remains viewable when analysis has no pantry context.
            return;
        }

        AnalysisLoading.IsVisible = true;
        AnalysisActivity.IsRunning = true;
        AnalysisStatus.IsVisible = false;

        try
        {
            _recipe = await _apiService.GetRecipeAnalysisAsync(
                _recipe.RecipeID,
                _pantryIngredients);
            BindingContext = _recipe;
        }
        // Keep the SQL recipe visible if the optional on-demand AI request fails.
        catch (Exception exception) when (
            exception is HttpRequestException or
                JsonException or
                OperationCanceledException or
                InvalidDataException)
        {
            AnalysisStatus.Text =
                "AI analysis is unavailable. You can still view the recipe information.";
            AnalysisStatus.IsVisible = true;
        }
        finally
        {
            AnalysisActivity.IsRunning = false;
            AnalysisLoading.IsVisible = false;
        }
    }

    // PopAsync takes this page off the top and shows the recipes page again
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    // opens the recipe's website in the web browser
    private async void OnOpenSourceClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_recipe.SourceUrl))
        {
            await Launcher.OpenAsync(_recipe.SourceUrl);
        }
    }

    // Opens the step-by-step cooking page
    private async void OnCookAlongClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new CookingStepsView(_recipe));
    }
}
