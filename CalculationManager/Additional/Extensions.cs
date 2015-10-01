using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManager.Additional
{
    public static class Extensions
    {
        public static string GetFullText(this Exception ex, bool includecallStack = true)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Exception ({0}) has occured from '{1}'. Target site: '{2}'", ex.GetType(), ex.Source, ex.TargetSite));
            sb.AppendLine(string.Format("Exception message: '{0}'", ex.Message));

            if (ex.Data != null && ex.Data.Keys.Count > 0)
            {
                sb.AppendLine(string.Format("Exception data:"));
                foreach (var key in ex.Data.Keys)
                    sb.AppendLine(string.Format("'{0}'='{1}'", key, ex.Data[key]));
            }
            if (includecallStack)
                sb.AppendLine(string.Format("Exception call stack:{0}{1}", Environment.NewLine, ex.StackTrace));
            
            if (ex.InnerException != null)
                sb.AppendLine(string.Format("Exception has inner exception:{0}{1}", Environment.NewLine, ex.InnerException.GetFullText(false)));

            return sb.ToString().Trim();
        }
    }
}
