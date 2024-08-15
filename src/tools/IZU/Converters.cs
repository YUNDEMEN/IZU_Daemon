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
        SolidColorBrush normal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991BD615"));//green
        SolidColorBrush abnormal = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99C1C1C1"));//gray
        SolidColorBrush warning = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99D61919"));//red
        SolidColorBrush mid = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#99D8D016"));//yellow
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
                    case "switch":
                        {
                            if ((bool)value)
                                return normal;
                            else
                                return warning;
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

    public class TranslateDoorStatusConverter : IValueConverter
    {
        string ToName(int status)
        {
            return status switch
            {
                3 => "Opened",
                2 => "Opening",
                1 => "Closing",
                0 => "Closed",
                _ => "--",
            };
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int status = (int)value;
            return ToName(status);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class TranslateDoorErrorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string binary = System.Convert.ToString((int)value, 2).PadLeft(8, '0');
            return OHTC.Tools.Tools.ErrText.GetErrText(binary);// + $" ({binary})"
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class TranslateBinaryDoorErrorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string binary = System.Convert.ToString((int)value, 2).PadLeft(8, '0');
            return binary;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
}
