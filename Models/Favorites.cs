using PantryPal.Models;
using System.Collections.ObjectModel;

namespace PantryPal.Models;

public class Favorites{

    public ObservableCollection<Recipe> RecipesFavorites { get; set; }

    public Favorites()
    {
        RecipesFavorites = new ObservableCollection<Recipe>
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
