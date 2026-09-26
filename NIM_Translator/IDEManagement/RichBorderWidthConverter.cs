using System;
using System.Globalization;
using System.Windows.Data;

namespace NIM
{
    public class RichBorderWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] Values, Type TargetType, object Parameter, CultureInfo Culture)
        {
            double Rich_h = (double)Values[0];
            double Border_h = (double)Values[1];
            double Border_w = (double)Values[2];
            if (Rich_h + 2 > Border_h)
            {
                return Border_w - 20;
            }
            else
            {
                return Border_w;
            }
        }

        public object[] ConvertBack(object Value, Type[] TargetTypes, object Parameter, CultureInfo Culture)
        {
            throw new NotImplementedException();
        }
    }
}
