using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CalculationManagerApplication.Converters
{
    public class DatesBetweenConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values != null)
            {
                var dates = values.OfType<DateTime>().Take(2);
                if (dates.Count() == 2)
                {
                    var mDate = dates.Max();
                    var lDate = dates.Min();
                    return (TimeSpan)(mDate - lDate);
                }
            }
            return new TimeSpan(0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
