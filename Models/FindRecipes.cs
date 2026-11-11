using PantryPal.Models;
using System.Collections.ObjectModel;

namespace PantryPal.Models;

public class FindRecipes
{
    public ObservableCollection<Recipe> Recipes { get; set; }

    public FindRecipes()
    {
        Recipes = new ObservableCollection<Recipe>
        {
            new Recipe
            {
                FavoriteStar="star.png",
                Image="image.jpg",
                Name = "Burrito",
                Rating="10/10",
                Mark="checkmark.jpg",
                Description = "Yummy yummy"
            },
            new Recipe
            {
                FavoriteStar="star.png",
                Image="image.jpg",
                Name = "Not burrito",
                Rating="10/10",
                Mark="checkmark.jpg",
                Description = "Not yummy"
            },
            new Recipe
            {
                FavoriteStar="star.png",
                Image="image.jpg",
                Name = "BurritoA",
                Rating="10/10",
                Mark="checkmark.jpg",
                Description = "AYummy yummy"
            },
            new Recipe
            {
                FavoriteStar="star.png",
                Image="image.jpg",
                Name = "Not burritoA",
                Rating="10/10",
                Mark="checkmark.jpg",
                Description = "ANot yummy"
            },
            new Recipe
            {
                FavoriteStar="star.png",
                Image="image.jpg",
                Name = "Not burritoB",
                Rating="10/10",
                Mark="checkmark.jpg",
                Description = "BNot yummy"
            }
        };
    }
}