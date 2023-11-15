using System.Text.Json;
using System.Timers;
using Newtonsoft.Json;


namespace Logger
{

    internal enum FileStatus
    {
        Valid,
        Invalid,
        NotFound,
        CantRead,
        NoAccess
    }
    public enum LogResult
    {
        Ok,
        NoAccess,
        FileNotFound
    }
    public enum DumpResult
    {
        Ok,
        NoAccess,
        FileNotFound
    }


    public class LoggerEventArgs : EventArgs
    {
        public string? Text { get; set; }
        public object? Object { get; set; }

        public LoggerEventArgs(string? text = null, object? obj = null)
        {
            Text = text;
            Object = obj;
        }

    }
    public class LoggerResponseEventArgs : EventArgs
    {
        public string Message { get; set; }

        public LoggerResponseEventArgs(string? message = null)
        {
            Message = message;
        }
    }
    public class Logger : IDisposable
    {
        private TimeSpan _loggerWait = TimeSpan.FromSeconds(5);
        private DateTime _lastLogRequest;
        public System.Timers.Timer _timer = new();
        private bool _disposed = false;
        private readonly string _file;
        public string LogFile { get { return _file; } }
        private readonly StreamWriter _logWwriter;
        private readonly JsonSerializerOptions _options;
        public EventHandler<LoggerResponseEventArgs> LoggerCallback { get; set; }
        public void DoLoggerCallback(object? sender, LoggerResponseEventArgs e)
        {
            var handler = LoggerCallback;
            handler?.Invoke(sender, e);
        }
        private List<LogData> _logRequests = new();
        private EventHandler<List<LogData>> _flushHandler;
        private void DoFlush(List<LogData> logRequests)
        {
            var handler = _flushHandler;
            handler?.Invoke(null, logRequests);
        }
        private bool FlushLock = false;
        public EventHandler<string> ReportLastObjectDump;
        public void DoReportLastObjectDump(object? sender, string e)
        {
            var handler = ReportLastObjectDump;
            handler?.Invoke(sender, e);
        }

        private class LogData
        {
            public string Message { get; set; }
            public DateTime Time { get; set; }
            public object Sender { get; set; }
            public int? newLines {  get; set; }
            public LogData()
            {
                
            }
        }


        public Logger(string file)
        {
            var status = _CheckFileValid(file);

            if (status is FileStatus.Valid)
            {
                _file = file;
                _logWwriter = new StreamWriter(file, true) { AutoFlush = true };
                _options = new JsonSerializerOptions() { WriteIndented = true };
                _flushHandler += HandleFlush;
                _timer.Elapsed += _timer_Elapsed;
            }
            else
            {
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = "Logfile is invalid!" });
                throw new ArgumentException(message: $"Invalid file, reason: {status.ToString()}");
            }



        }

        private void _timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            switch (FlushLock)
            {
                case true:
                    return;
                case false:
                    var diff = e.SignalTime - _lastLogRequest;
                    if (diff >= _loggerWait && _logRequests.Count > 0)
                    {
                        FlushLock = true;
                        LogData[] data = new LogData[_logRequests.Count];
                        _logRequests.CopyTo(data);
                        DoFlush(data.ToList());
                    }
                    return;
            }

        }


        private FileStatus _CheckFileValid(string file)
        {
            try
            {
                using (var f = File.OpenRead(file))
                {
                    _ = f.ReadByte();
                    return FileStatus.Valid;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return FileStatus.NoAccess;
            }
            catch (FileNotFoundException)
            {
                File.Create(file).Close();
                return FileStatus.Valid;
            }
        }
        

        private async void HandleFlush(object? sender, List<LogData> e)
        {
            foreach (var item in e)
            {
                var res = _Log(item.Sender, item.Message, item.newLines);
                
                if(res is LogResult.Ok)
                {
                    _logRequests.Remove(item);
                    //DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = item.Message });
                }
            }

            FlushLock = false;
        }


        private LogResult _Log(object? sender, string e, int? newLines)
        {
            DateTime now = DateTime.Now;
            string logText = $"[{now.ToString()}] -- [{sender}]: {e}";

            try
            {
                if(newLines != null)
                {
                    for (int i = 1; i < newLines; i++)
                    {
                        _logWwriter.WriteLine("\n");

                    }
                }

                _logWwriter.WriteLine(logText);
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = e });
                return LogResult.Ok;
            }
            catch (UnauthorizedAccessException ex)
            {
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = $"Trying to write to log file resulted in {nameof(ex)}" });
                return LogResult.NoAccess;
            }
            catch (FileNotFoundException ex)
            {
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = $"Trying to write to log file resulted in {nameof(ex)}" });
                return LogResult.FileNotFound;
            }

        }
        public async Task<LogResult> Log(object? sender, string e)
        {
            DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = e });
            _timer.Stop();
            _lastLogRequest = DateTime.Now;
            _logRequests.Add(new LogData() { Message = e, Sender = sender, Time = DateTime.Now });
            _timer.Start();
            return LogResult.Ok;
        }
        public async Task<LogResult> LogAppStart(object? sender, string e)
        {
            DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = e });
            _timer.Stop();
            _lastLogRequest = DateTime.Now;
            _logRequests.Add(new LogData() { Message = e, Sender = sender, Time = DateTime.Now, newLines = 2 });
            _timer.Start();
            return LogResult.Ok;

        }

        public async Task<DumpResult> DumpObject(object? sender, object e)
        {
            try
            { 
                string dumpText = JsonConvert.SerializeObject(e, Formatting.Indented);
                var tempDumpFile = Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".json";
                var tempDumpFolder = Path.GetTempPath();
                string tempFullPath = Path.Combine(tempDumpFolder, tempDumpFile);
                await File.WriteAllTextAsync(tempFullPath, dumpText);
                await Log(this, $"Dumped object to: {tempFullPath}");
                DoReportLastObjectDump(this, tempFullPath);
                return DumpResult.Ok;
            }
            catch (UnauthorizedAccessException ex)
            {
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = $"Trying to dump object resulted in {nameof(ex)}" });
                return DumpResult.NoAccess;
            }
            catch (FileNotFoundException ex)
            {
                DoLoggerCallback(this, new LoggerResponseEventArgs() { Message = $"Trying to dump object resulted in {nameof(ex)}" });
                return DumpResult.FileNotFound;
            }
        }



        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {

            if (!_disposed)
            {
                if (disposing)
                {
                    _logWwriter.Dispose();

                    _disposed = true;
                }
            }

        }
    }
}
