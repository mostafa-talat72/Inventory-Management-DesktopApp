using System;
using System.Windows;
using System.Windows.Controls;

namespace ProductApp.Views;

public partial class ShortcutsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    public ShortcutsDialog()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}
