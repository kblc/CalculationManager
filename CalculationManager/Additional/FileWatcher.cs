using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationManager.Additional
{
    public delegate void LogMessage(string message, Exception exception);

    public class FileWatcherParam
    {
        public string Folder { get; private set; }
        public string Filter { get; private set; }
        public string Command { get; private set; }
        public string LogCodepage { get; private set; }
        public FileWatcherParam(string folder, string filter, string command, string logCodepage)
        {
            this.Folder = folder;
            this.Filter = filter;
            this.Command = command;
            this.LogCodepage = logCodepage;
        }
    }

    public class FileWatcher : IDisposable
    {
        public FileWatcher(FileQueue fileQueue)
        {
            if (fileQueue == null)
                throw new ArgumentNullException("fileQueue", "Queue can't be null");
            Queue = fileQueue;
        }

        public FileQueue Queue { get; private set; }

        public IParameters Parameters { get; private set; }

        private List<System.IO.FileSystemWatcher> watcherList = new List<System.IO.FileSystemWatcher>();

        public LogMessage Log { get; set; }

        private void LogMessage(string message, Exception ex = null)
        {
            if (Log != null)
                Log(message, ex);
        }

        public void Start(IEnumerable<FileWatcherParam> allPrm)
        {
            watcherList.ForEach((w) => { w.EnableRaisingEvents = false; w.Dispose(); });
            watcherList.Clear();

            var rightPrm = allPrm.Where(p => System.IO.Directory.Exists(p.Folder));

            var getFileQueueElement = new Func<string, string, string, FileQueueElement>((fullPath, cmd, enc) =>
            {
                string num = new string(System.IO.Path.GetExtension(fullPath).Reverse().Take(2).Reverse().ToArray());
                string runCommand = cmd.Replace("??", num);
                return new FileQueueElement(fullPath, runCommand, enc);
            });

            Queue.Push(
                rightPrm
                    .SelectMany(p => System.IO.Directory.GetFiles(p.Folder, p.Filter)
                        .Select(fullPath => getFileQueueElement(fullPath, p.Command, p.LogCodepage) )
                    )
                );

            watcherList.AddRange(
                rightPrm
                .Select(p =>
                    {
                        var watcher = new System.IO.FileSystemWatcher(p.Folder, p.Filter);
                        watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

                        watcher.Created += (s, e) => { Queue.Push(getFileQueueElement(e.FullPath, p.Command, p.LogCodepage)); };
                        watcher.Changed += (s, e) => { Queue.Push(getFileQueueElement(e.FullPath, p.Command, p.LogCodepage)); };
                        watcher.EnableRaisingEvents = true;
                        return watcher;
                    })
                );

            var badFolders = string.Empty;
            foreach(var ef in allPrm.Except(rightPrm))
                badFolders += ef.Folder + "(" + ef.Filter + ");";
            if (!string.IsNullOrEmpty(badFolders))
                LogMessage(string.Format("not connected to folders: {0}", badFolders), new Exception("File watcher can't connect to this folders: " + badFolders));

            var folders = string.Empty;
            foreach (var f in rightPrm)
                folders += f.Folder + "(" + f.Filter + ");";
            LogMessage(string.Format("connected to folders: {0}", folders));
        }

        public void Stop()
        {
            watcherList.ForEach((w) => { w.EnableRaisingEvents = false; w.Dispose(); });
            watcherList.Clear();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
