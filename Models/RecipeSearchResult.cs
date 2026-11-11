using System.Net;

namespace PantryPal.Models;

// one recipe sent back by the api
// the property names match the api's json, capitals don't matter when it's read
public class RecipeSearchResult
{
    public string Title { get; set; } = "";
    public string ImageURL { get; set; } = "";
    public int ReadyInMinutes { get; set; }
    public int Servings { get; set; }

    // link to the website the recipe came from
    public string SourceUrl { get; set; } = "";

    // the steps, the database stores them as html like <ol><li>step</li></ol>
    public string Instructions { get; set; } = "";

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

    // turns the html steps into numbered plain text, one string per step
    public List<string> Steps
    {
        get
        {
            var steps = new List<string>();
            if (string.IsNullOrEmpty(Instructions)) return steps;

            // a step ends at </li> or </p>, so swap those for a new line
            string html = Instructions.Replace("</li>", "\n").Replace("</p>", "\n");

            // copies every letter except the ones inside < >, which removes the html tags
            string text = "";
            bool insideTag = false;
            foreach (char letter in html)
            {
                if (letter == '<') insideTag = true;
                else if (letter == '>') insideTag = false;
                else if (!insideTag) text += letter;
            }

            // turns codes like &amp; back into normal symbols like &
            text = WebUtility.HtmlDecode(text);

            // one numbered step per line, skipping blank lines
            foreach (string line in text.Split('\n'))
            {
                if (line.Trim() != "")
                {
                    steps.Add((steps.Count + 1) + ". " + line.Trim());
                }
            }
            return steps;
        }
    }
}
