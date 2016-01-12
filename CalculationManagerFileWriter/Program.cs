using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace CalculationManagerFileWriter
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var parameters = new ParametersContainer();
                var outputFile = parameters.GetParameter("OutputFile");
                var codepage = Encoding.GetEncoding(parameters.GetParameter("Encoding") ?? Encoding.Default.EncodingName);
                var db = parameters.GetParameter("DB");

                var inputFile = string.Empty;
                var hour = string.Empty;
                var day = string.Empty;

                foreach (var a in args)
                {
                    if (a.StartsWith("hour:"))
                        hour = a.Substring("hour:".Length);
                    
                    if (a.StartsWith("day:"))
                        day = a.Substring("day:".Length);
                }

                if (!System.IO.File.Exists(outputFile))
                    throw new Exception("Output file not specified or not exists. Use key 'OutputFile' in app.config!");

                if (!string.IsNullOrEmpty(day))
                    inputFile = (parameters.GetParameter("WatchFolder_Day") ?? string.Empty).Replace("??", day); else
                    inputFile = (parameters.GetParameter("WatchFolder_Hour") ?? string.Empty).Replace("??", hour);

                if (!System.IO.File.Exists(inputFile))
                    throw new Exception(string.Format("Input file not specified or not exists: '{0}'", inputFile));

                if (string.IsNullOrEmpty(db))
                    throw new Exception("DB not specified. Use key 'DB' in app.config");

                if (string.IsNullOrEmpty(day) && string.IsNullOrEmpty(hour))
                    throw new Exception("Day or hour not specified!");

                var ci = new CultureInfo("ru-RU");
                var creationDate = GetPZGFileDate(inputFile, "dd.MM.yyyy", ci);

                //creationDate = new DateTime(2015, 11, 01);

                string fDate = string.Empty;
                string lDate = string.Empty;

                if (!string.IsNullOrEmpty(day))
                {
                    #region Example
                    //(char:3)tom(\r)
                    //24ч09.02.2015(char:16) 9 Февраль 2015г
                    //24ч21.09.2015(char:16)21 Сентябрь2015г
                    #endregion
                    if (string.IsNullOrEmpty(hour))
                        hour = "24";
                    fDate = creationDate.ToString("dd.MM.yyyy");

                    var lDayMonth = creationDate.ToString("MMMM", ci).Substring(0, 1).ToUpper() + creationDate.ToString("MMMM", ci).Substring(1);

                    switch (creationDate.Month)
                    {
                        //Специальные настройки для ноября. Должен быть такой формат: '_1_Ноябрь__2015г'
                        case 1:
                        case 11:
                            while (lDayMonth.Length < 8)
                                lDayMonth += " ";
                            break;

                        //Обычные настройки для всех остальных. Должен быть такой формат: '_1____Март_2015г'
                        default:

                            while (lDayMonth.Length < 7)
                                lDayMonth = " " + lDayMonth;
                            if (lDayMonth.Length < 8)
                                lDayMonth += " ";
                            break;
                    }

                    //while (lDayMonth.Length < 8)
                    //    lDayMonth += " ";

                    lDate = creationDate.ToString("dd "+ lDayMonth + "yyyy", ci);

                    if (fDate[0] == '0')
                        fDate = " " + fDate.Remove(0, 1);
                    if (lDate[0] == '0')
                        lDate = " " + lDate.Remove(0, 1);

                    fDate = string.Format("{0}ч{1}", hour, fDate);
                    lDate = string.Format("{1}г", hour, lDate);
                }
                else
                {
                    #region Example
                    //(char:3)tom(\r)
                    // 6ч 9.02.2015(char:16) 6ч  9 Фев 2015г
                    #endregion
                    hour = int.Parse(hour).ToString("00");
                    if (hour[0] == '0')
                        hour = " " + hour.Remove(0, 1);

                    fDate = string.Format("{0}", creationDate.ToString("dd.MM.yyyy", ci));
                    lDate = string.Format("{0} {1} {2}", creationDate.ToString("dd", ci), creationDate.ToString("MMM", ci).Substring(0, 1).ToUpper() + creationDate.ToString("MMM", ci).Substring(1), creationDate.ToString("yyyy", ci));

                    if (fDate[0] == '0')
                        fDate = " " + fDate.Remove(0, 1);
                    if (lDate[0] == '0')
                        lDate = " " + lDate.Remove(0, 1);

                    fDate = string.Format("{0}ч{1}", hour, fDate);
                    lDate = string.Format("{0}ч {1}г", hour, lDate);
                }

                string fileTemplate = string.Format("{0}{1}\r{2}{3}{4}", (char)(db.Length), db, fDate, (char)16, lDate);
                Console.WriteLine("Write to file '{0}' this text:\n{1}", outputFile, fileTemplate);
                System.IO.File.WriteAllText(outputFile, fileTemplate, codepage);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception cathed: {0}", ex.Message);
                Console.WriteLine("Use this executable with params: [hour:HOUR|day:DAY]");
                Console.WriteLine("examples:");
                Console.WriteLine("\t\tapp.exe hour:06");
                Console.WriteLine("\t\tapp.exe day:01 hour:24");
                throw ex;
            }
        }

        static DateTime GetPZGFileDate(string filePath, string format, CultureInfo ci)
        {
            if (System.IO.File.Exists(filePath))
            {
                var dateString = System.IO.File.ReadLines(filePath)
                    .First()
                    .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .First();
                return DateTime.ParseExact(dateString, format, ci);
            }
            else
                throw new Exception(string.Format("File not exists or access denied: '{0}'", filePath));
        }
    }
}
