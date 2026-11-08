using System.Text.Json.Serialization;

namespace PantryPal.Models;

public class PantryItem
{
    public string Name { get; set; } = "";
    public string? Brand { get; set; }
    public double Price { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;

    // [JsonIgnore] keeps them out of pantry.json.
    [JsonIgnore]
    public string BrandLabel => string.IsNullOrWhiteSpace(Brand) ? "No brand" : $"Brand: {Brand}";

    [JsonIgnore]
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[0].ToString().ToUpper();
}
