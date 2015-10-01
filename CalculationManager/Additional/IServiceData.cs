using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManager.Additional
{
    public interface IServiceData
    {
        IParameters Parameters { get; }
        FileQueue Queue { get; }
        FileWatcher Watcher { get; }
        FileCalculation Calculation { get; }
    }
}
