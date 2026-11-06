using PantryPal.ViewModels;

namespace PantryPal.Views;

public partial class FindRecipes : ContentPage
{
	public FindRecipes(RecipeSearchViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}