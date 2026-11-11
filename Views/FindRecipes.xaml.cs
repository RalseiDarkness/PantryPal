using PantryPal.ViewModels;
using PantryPal.Models;

namespace PantryPal.Views;

public partial class FindRecipes : ContentPage
{
	public FindRecipes(RecipeSearchViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	private void OnSuggestionSelected(
		object sender,
		SelectionChangedEventArgs e)
	{
		if (BindingContext is RecipeSearchViewModel viewModel &&
			e.CurrentSelection.FirstOrDefault() is IngredientSuggestion ingredient)
		{
			viewModel.SelectIngredient(ingredient);
		}

		if (sender is CollectionView collectionView)
		{
			collectionView.SelectedItem = null;
		}
	}

	private void OnRemoveIngredientClicked(object sender, EventArgs e)
	{
		if (BindingContext is RecipeSearchViewModel viewModel &&
			sender is Button { BindingContext: IngredientSuggestion ingredient })
		{
			viewModel.RemoveIngredient(ingredient);
		}
	}

	private void OnToggleInstructionsClicked(object sender, EventArgs e)
	{
		if (BindingContext is RecipeSearchViewModel viewModel &&
			sender is Button { BindingContext: RecipeSearchResult recipe })
		{
			viewModel.ToggleInstructionsCommand.Execute(recipe);
		}
	}

	private void OnOpenSourceClicked(object sender, EventArgs e)
	{
		if (BindingContext is RecipeSearchViewModel viewModel &&
			sender is Button { BindingContext: RecipeSearchResult recipe })
		{
			viewModel.OpenRecipeSourceCommand.Execute(recipe);
		}
	}
}