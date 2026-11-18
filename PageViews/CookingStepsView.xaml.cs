using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using PantryPal.Models;
using System;

namespace PantryPal.PageViews;

/// <summary>
/// Presents every stored instruction in a scrollable sequence and links back to
/// the source recipe when its URL is valid.
/// </summary>
public partial class CookingStepsView : ContentPage
{
    private readonly RecipeSearchResult _recipe;

    ///Builds the instruction cards from the selected recipe's HTML steps.
    public CookingStepsView(RecipeSearchResult recipe)
    {
        InitializeComponent();

        _recipe = recipe;

        RecipeTitle.Text = recipe.Title;

        // The shared model parser handles source HTML and entities before each
        // returned line is rendered as its own numbered card.
        var steps = recipe.GetInstructionSteps();

        NoInstructionsLabel.IsVisible = steps.Count == 0;
        StepsLayout.IsVisible = steps.Count > 0;

        for (var index = 0; index < steps.Count; index++)
        {
            var instructionHeading = new Label
            {
                Text = $"Instruction {index + 1}",
                FontSize = 16,
                FontFamily = "OpenSansSemibold"
            };
            instructionHeading.SetDynamicResource(
                Label.TextColorProperty,
                "PantryOrange");

            var instructionText = new Label
            {
                Text = steps[index],
                FontSize = 17,
                LineBreakMode = LineBreakMode.WordWrap
            };
            instructionText.SetDynamicResource(
                Label.TextColorProperty,
                "PantryText");

            var instructionContent = new VerticalStackLayout
            {
                Spacing = 8,
                Children = { instructionHeading, instructionText }
            };

            var instructionCard = new Border
            {
                Padding = 18,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = 14
                },
                Content = instructionContent
            };

            instructionCard.SetDynamicResource(
                Border.BackgroundColorProperty,
                "PantryCard");

            StepsLayout.Children.Add(instructionCard);
        }

        // Only expose external navigation for absolute web URLs.
        SourceButton.IsVisible =
            Uri.TryCreate(recipe.SourceUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps);
    }

    private async void OnSourceClicked(object? sender, EventArgs e)
    {
        if (Uri.TryCreate(_recipe.SourceUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        {
            await Launcher.OpenAsync(uri);
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
