using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Lunaqua.Components
{
    public partial class PluginItem : UserControl
    {
        public PluginItem()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
