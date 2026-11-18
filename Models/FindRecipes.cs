using PantryPal.Models;
using System.Collections.ObjectModel;

namespace PantryPal.Models;

/// Prototype in-memory recipe collection used by the legacy sample view.
public class FindRecipes
{
    public ObservableCollection<Recipe> Recipes { get; set; }

    public FindRecipes()
    {
        Recipes = new ObservableCollection<Recipe>
        {
            new Recipe
            {
                Name = "Burrito",
                Description = "Yummy yummy"
            },
            new Recipe
            {
                Name = "Not burrito",
                Description = "Not yummy"
            }
        };
    }
}