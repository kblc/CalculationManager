using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManager.Additional
{
    //EventIDs
    public enum Subcategoryes
    {
        Loading = 0,
        Unloading = 1,
        InAction = 2
    }

    //EventIDs
    public enum ErrorCodes
    {
        CalculationManagerService_CtyticalError = 100,
        CalculationManagerService_Error = 101
    }
}
