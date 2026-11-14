using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using PantryPal.Models;

namespace PantryPal.PageViews;

public partial class CookingStepsView : ContentPage
{
    private readonly List<string> _steps;

    public CookingStepsView(RecipeSearchResult recipe)
    {
        InitializeComponent();

        _steps = recipe.Steps ?? new List<string>();

        RecipeTitle.Text = recipe.Title;

        StepsCarousel.ItemsSource = _steps;

        StepsCarousel.Position = 0;

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        int position = StepsCarousel.Position;

        StepCounter.Text = $"Step {position + 1} of {_steps.Count}";

        PreviousButton.IsEnabled = position > 0;

        NextButton.Text =
            position == _steps.Count - 1 ? "Finish" : "Next Step";
    }

    private void OnPositionChanged(
        object? sender,
        PositionChangedEventArgs e)
    {
        UpdateButtons();
    }

    private void OnPreviousClicked(object? sender, EventArgs e)
    {
        if (StepsCarousel.Position > 0)
        {
            StepsCarousel.ScrollTo(
                StepsCarousel.Position - 1,
                position: ScrollToPosition.Center,
                animate: true);
        }
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (StepsCarousel.Position < _steps.Count - 1)
        {
            StepsCarousel.ScrollTo(
                StepsCarousel.Position + 1,
                position: ScrollToPosition.Center,
                animate: true);
        }
        else
        {
            await DisplayAlertAsync(
                "Finished",
                "",
                "Done");

            await Navigation.PopAsync();
        }
    }

    private async void OnBackClicked(
        object? sender,
        EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
