using OHTC.Tools.OHTCommand;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SamplesCommon
{
    internal class ListBoxTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            DependencyObject? parent = container;
            while (true)
            {
                parent = VisualTreeHelper.GetParent(parent);
                if (parent is ListBox)
                    break;
                if (parent == null)
                    break;
            }
            if (parent != null && parent is ListBox element && item != null && item is OhtModel oht)
            {
                switch (oht.Mode)
                {
                    case "move":
                    case "carry":
                       return element.FindResource("move") as DataTemplate;
                    case "load":
                        return element.FindResource("load") as DataTemplate;
                    default:
                        break;
                }
            }

            return null;
        }
    }
}
