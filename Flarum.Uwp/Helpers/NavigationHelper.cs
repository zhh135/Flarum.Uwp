﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;

namespace Flarum.Helpers
{
    public class NavigationHelper
    {
        public static string GetNavigateTo(NavigationViewItem item) => (string)item.GetValue(NavigateToProperty);

        public static void SetNavigateTo(NavigationViewItem item, string value) => item.SetValue(NavigateToProperty, value);

        public static readonly DependencyProperty NavigateToProperty =
        DependencyProperty.RegisterAttached("NavigateTo", typeof(Page), typeof(NavigationHelper), new PropertyMetadata(null));
    }
}
