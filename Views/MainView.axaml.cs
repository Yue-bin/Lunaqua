using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Lunaqua.Services;

namespace Lunaqua.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        ViewModelLocator.Instance.Provider.GetRequiredService<NavigationService>().RegisterHandler(Navigate);
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Navigate(typeof(HomeView));
    }

    private void Navigate(Type pageType, object? parameter = null, NavigationTransitionInfo? transition = null)
    {
        if (Root.Content?.GetType() == pageType)
            return;

        if (transition != null)
        {
            Root.Navigate(pageType, parameter, transition);
        }
        else if (parameter != null)
        {
            Root.Navigate(pageType, parameter);
        }
        else
        {
            Root.Navigate(pageType);
        }
    }

    private void Navigation_OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        ViewModelLocator.Instance.Provider.GetRequiredService<NavigationService>()
            .Navigate((Type)e.InvokedItemContainer.Tag!, e.RecommendedNavigationTransitionInfo);
    }
}