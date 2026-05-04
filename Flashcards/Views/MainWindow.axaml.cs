using System;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Flashcards.Models;
using Flashcards.ViewModels;

namespace Flashcards.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PointerPressed += OnWidgetPointerPressed;
        Closed += OnClosed;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            // Replace the MinimizeToTrayCommand with one that handles window minimize
            viewModel.MinimizeToTrayCommand = new RelayCommand(() =>
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = true;
            });

            // Find and configure the AutoCompleteBox
            var autoCompleteBox = this.FindControl<AutoCompleteBox>("SearchBox");
            if (autoCompleteBox != null)
            {
                autoCompleteBox.SelectionChanged += (sender, e) =>
                {
                    if (e.AddedItems.Count > 0 && e.AddedItems[0] is FlashcardEntry flashcard)
                    {
                        // Execute the command to select the flashcard
                        viewModel.SelectSearchResultCommand.Execute(flashcard);
                        // Close the dropdown to prevent issues
                        try
                        {
                            autoCompleteBox.IsDropDownOpen = false;
                        }
                        catch
                        {
                            // Ignore errors when closing dropdown
                        }
                    }
                };
            }
        }
    }

    private void OnWidgetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }
    }
}
