using CalculationManagerServiceTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManagerConsoleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var sc = new ServiceTest();
            sc.ServiceTest_WCFAndCalculationAndWatcherAndCommand(60*60*1000);
            Console.WriteLine("Press [ENTER] for exit");
            Console.ReadLine();
        }
    }
}
