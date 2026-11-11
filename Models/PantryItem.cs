using System.Text.Json.Serialization;

namespace PantryPal.Models;

// one ingredient in the pantry, these are what get saved in pantry.json
public class PantryItem
{
    public string Name { get; set; } = "";
    public string? Brand { get; set; } // ? means it can be empty (null)
    public double Price { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;

    // the two below are only for showing on screen
    // [JsonIgnore] keeps them out of pantry.json

    // the grey line under the name, like Brand: Kroger
    [JsonIgnore]
    public string BrandLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Brand)) return "No brand";
            return "Brand: " + Brand;
        }
    }

    // the big letter in the orange square, like C for chicken
    [JsonIgnore]
    public string Initial
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "?";
            return Name.Trim().Substring(0, 1).ToUpper(); // the first letter, made uppercase
        }
    }
}
