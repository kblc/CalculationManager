using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime.Serialization;

namespace CalculationManager.Additional
{
    [DataContract]
    public class FileQueueElement
    {
        [DataMember]
        public string File { get; set; }
        [DataMember]
        public string Command { get; set; }
        [DataMember]
        public DateTime Changed { get; set; }
        [DataMember]
        public string LogEncoding { get; set; }

        public FileQueueElement() { }
        public FileQueueElement(string file, string command, string logEncoding)
        {
            this.File = file;
            this.Command = command;
            this.Changed = GetChanged();
            this.LogEncoding = logEncoding;
        }

        public DateTime GetChanged()
        {
            return System.IO.File.Exists(File)
                    ? System.IO.File.GetLastWriteTimeUtc(File)
                    : DateTime.MinValue;
        }
    }

    [DataContract(Name = "FileQueueAction")]
    public enum FileQueueAction 
    { 
        [EnumMember]
        Push = 0,
        [EnumMember]
        Pop = 1 
    }

    [DataContract]
    public class FileQueueChangedArgs
    {
        [DataMember]
        public FileQueueElement Element { get; set; }
        [DataMember]
        public FileQueueAction Action { get; set; }
        public FileQueueChangedArgs(FileQueueElement element, FileQueueAction action)
        {
            this.Element = element;
            this.Action = action;
        }
        public FileQueueChangedArgs() { }
    }

    public class FileQueue
    {
        private object lockListObject = new Object();
        private List<FileQueueElement> fileQueue = new List<FileQueueElement>();

        public void Push(IEnumerable<FileQueueElement> els)
        {
            foreach (var el in els)
                Push(el);
        }

        public void Push(FileQueueElement el)
        {
            lock(lockListObject)
            { 
                var oldItem = fileQueue.FirstOrDefault(e => e.File == el.File);
                if (oldItem != null)
                { 
                    fileQueue.Remove(oldItem);
                    RaiseChanged(oldItem, FileQueueAction.Pop);
                }
                fileQueue.Add(el);
                RaiseChanged(el, FileQueueAction.Push);
            }
        }

        public FileQueueElement Pop()
        {
            FileQueueElement result = null;
            lock (lockListObject)
            {
                result = fileQueue.OrderBy(i => i.Changed).FirstOrDefault();
                if (result != null)
                { 
                    fileQueue.Remove(result);
                    RaiseChanged(result, FileQueueAction.Pop);
                }
            }
            return result;
        }

        public void Clear()
        {
            while (Pop() != null) ;
        }

        private void RaiseChanged(FileQueueElement element, FileQueueAction action)
        {
            var itemElement = element;
            var itemAction = action;
            if (Changed != null)
                Changed(this, new FileQueueChangedArgs(itemElement, itemAction));
        }

        public List<FileQueueElement> GetQueue()
        {
            lock (lockListObject)
                return new List<FileQueueElement>(fileQueue);
        }

        public event EventHandler<FileQueueChangedArgs> Changed;

        public int Count 
        { 
            get 
            {
                lock (lockListObject)
                {
                    return fileQueue.Count;
                }
            } 
        }
    }
}
