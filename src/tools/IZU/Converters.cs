using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OHTC.Tools
{
    public class BooleanReverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TrueColorConverter : IValueConverter
    {
        SolidColorBrush normal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00F100"));//green
        SolidColorBrush abnormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCECECE"));//gray
        SolidColorBrush warning = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF0000"));//red
        SolidColorBrush mid = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFF500"));//yellow
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null)
            {
                switch (parameter.ToString())
                {
                    case "warn":
                        {
                            if ((bool)value)
                                return warning;
                            else
                                return abnormal;
                        }
                    case "flash":
                        {
                            if ((bool)value)
                                return normal;
                            else
                                return mid;
                        }
                    default: return abnormal;
                }
            }
            else
            {
                if ((bool)value)
                    return normal;
                else
                    return abnormal;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
