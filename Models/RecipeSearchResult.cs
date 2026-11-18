using System.Net;
using System.Text.RegularExpressions;

namespace PantryPal.Models;

// one recipe sent back by the api
// the property names match the api's json, capitals don't matter when it's read
/// Client-side recipe contract deserialized from PantryPal.Api, including SQL
/// matches and optional AI fields used by the recipe detail page.
public class RecipeSearchResult
{
    /// <summary>Stable recipe ID used for detail-analysis requests.</summary>
    public int RecipeID { get; set; }
    public string Title { get; set; } = "";
    public string? ImageURL { get; set; }
    public int? ReadyInMinutes { get; set; }
    public int? Servings { get; set; }

    // link to the website the recipe came from
    public string? SourceUrl { get; set; }

    // the steps, the database stores them as html like <ol><li>step</li></ol>
    public string? Instructions { get; set; }

    /// <summary>Counts and percentage used to describe the pantry match.</summary>
    public int MatchingIngredientCount { get; set; }
    public int MissingIngredientCount { get; set; }
    public decimal MatchPercentage { get; set; }
    public string? Overview { get; set; }
    public string? WhyItMatches { get; set; }
    public string? MatchAssessment { get; set; }
    public string? Difficulty { get; set; }
    public string? RegionOfOrigin { get; set; }
    public string? MealHeaviness { get; set; }
    public string? MealHeavinessExplanation { get; set; }
    public List<string>? PotentialAllergens { get; set; } = new();
    public string? AllergenAssessment { get; set; }
    public List<AllergenExplanation>? AllergenExplanations { get; set; } = new();
    public List<RecipeOriginSource>? OriginSources { get; set; } = new();
    public string? OtherConsiderations { get; set; }
    public List<string> Substitutions { get; set; } = new();

    // pantry ingredients this recipe uses
    public List<string> MatchingIngredients { get; set; } = new List<string>();

    // ingredients the recipe needs that are not in the pantry
    public List<string> MissingIngredients { get; set; } = new List<string>();

    // everything below is made from the data above for the pages to show
    // they only have get, so the json reader skips them

    // short line under the title, like 45 min, serves 4
    public string Summary
    {
        get { return ReadyInMinutes + " min, serves " + Servings; }
    }

    // how well it fits the pantry, shown on the recipe card
    public string MatchText
    {
        get { return "Uses " + MatchingIngredients.Count + " of your ingredients, needs " + MissingIngredients.Count + " more"; }
    }

    // the ingredient lists as one line each, Join puts ", " between the names
    public string HaveText
    {
        get { return string.Join(", ", MatchingIngredients); }
    }

    public string NeedText
    {
        get { return string.Join(", ", MissingIngredients); }
    }

    // true when there is a photo or link, IsVisible in the xaml uses these
    // IsNullOrEmpty also catches when the database sent nothing at all
    public bool HasImage
    {
        get { return !string.IsNullOrEmpty(ImageURL); }
    }

    public bool HasSource
    {
        get { return !string.IsNullOrEmpty(SourceUrl); }
    }

    public bool HasOverview
    {
        get { return !string.IsNullOrWhiteSpace(Overview); }
    }

    public bool HasWhyItMatches
    {
        get { return !string.IsNullOrWhiteSpace(WhyItMatches); }
    }

    public bool HasMatchAssessment
    {
        get { return !string.IsNullOrWhiteSpace(MatchAssessment); }
    }

    public bool HasDifficulty
    {
        get { return !string.IsNullOrWhiteSpace(Difficulty); }
    }

    public bool HasRegionOfOrigin
    {
        get { return !string.IsNullOrWhiteSpace(RegionOfOrigin); }
    }

    public bool HasOriginSources
    {
        get { return !string.IsNullOrWhiteSpace(OriginSourcesText); }
    }

    public string OriginSourcesText
    {
        get
        {
            return string.Join(
                Environment.NewLine,
                OriginSources?
                    .Where(source =>
                        source is not null &&
                        !string.IsNullOrWhiteSpace(source.Title) &&
                        !string.IsNullOrWhiteSpace(source.Url))
                    .Select(source => $"{source.Title}: {source.Url}") ??
                Enumerable.Empty<string>());
        }
    }

    public bool HasMealHeaviness
    {
        get { return !string.IsNullOrWhiteSpace(MealHeavinessText); }
    }

    public string MealHeavinessText
    {
        get
        {
            var details = new[] { MealHeaviness, MealHeavinessExplanation }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            return string.Join(": ", details);
        }
    }

    public bool HasPotentialAllergens
    {
        get { return !string.IsNullOrWhiteSpace(PotentialAllergensText); }
    }

    public string PotentialAllergensText
    {
        get
        {
            var allergens = PotentialAllergens is { Count: > 0 }
                ? string.Join(", ", PotentialAllergens)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(allergens))
            {
                return AllergenAssessment ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(AllergenAssessment)
                ? allergens
                : allergens + ". " + AllergenAssessment;
        }
    }

    public bool HasAllergenExplanations
    {
        get { return !string.IsNullOrWhiteSpace(AllergenExplanationsText); }
    }

    public string AllergenExplanationsText
    {
        get
        {
            return string.Join(
                Environment.NewLine,
                AllergenExplanations?
                    .Where(explanation =>
                        explanation is not null &&
                        !string.IsNullOrWhiteSpace(explanation.Allergen) &&
                        !string.IsNullOrWhiteSpace(explanation.TriggeringIngredient) &&
                        !string.IsNullOrWhiteSpace(explanation.Explanation))
                    .Select(explanation =>
                        $"{explanation.Allergen} ({explanation.Certainty}) - " +
                        $"{explanation.TriggeringIngredient}: {explanation.Explanation}") ??
                Enumerable.Empty<string>());
        }
    }

    public bool HasOtherConsiderations
    {
        get { return !string.IsNullOrWhiteSpace(OtherConsiderations); }
    }

    public bool HasSubstitutions
    {
        get { return Substitutions.Count > 0; }
    }

    public bool HasMatchingIngredients
    {
        get { return MatchingIngredients.Count > 0; }
    }

    public bool HasMissingIngredients
    {
        get { return MissingIngredients.Count > 0; }
    }

    public string SubstitutionsText
    {
        get { return string.Join(", ", Substitutions); }
    }

    // turns the html steps into numbered plain text, one string per step
    public List<string> Steps
    {
        get
        {
            return GetInstructionSteps()
                .Select((step, index) => $"{index + 1}. {step}")
                .ToList();
        }
    }

    public sealed class RecipeOriginSource
    {
        public string Title { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;
    }

    public sealed class AllergenExplanation
    {
        public string Allergen { get; set; } = string.Empty;

        public string TriggeringIngredient { get; set; } = string.Empty;

        public string Explanation { get; set; } = string.Empty;

        public string Certainty { get; set; } = string.Empty;
    }

    /// <summary>
    /// Converts the database's HTML instruction text into readable individual
    /// lines for the vertically scrolling cooking page.
    /// </summary>
    public List<string> GetInstructionSteps()
    {
        if (string.IsNullOrWhiteSpace(Instructions))
        {
            return [];
        }

        // SQL instructions commonly contain list/paragraph markup. Preserve its
        // line boundaries before stripping tags, then decode entities for display.
        var text = Regex.Replace(
            Instructions,
            @"<\s*br\b[^>]*>",
            "\n",
            RegexOptions.IgnoreCase);
        text = Regex.Replace(
            text,
            @"</?\s*(?:li|p|div|ol|ul|section|article|h[1-6]|blockquote|pre|tr|td)\b[^>]*>",
            "\n",
            RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]*>", string.Empty);
        text = WebUtility.HtmlDecode(text);

        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(step => Regex.Replace(step, @"\s+", " ").Trim())
            .Where(step => step.Length > 0)
            .ToList();
    }
}
