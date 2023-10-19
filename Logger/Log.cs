using System.Security.Authentication;

namespace Logger
{
    public class Log
    {
        private string _logFile;
        public string LogFile
        {
            get { return _logFile; }
        }


        private IndentRules _indentRule;
        public IndentRules IndentRule
        {
            get { return _indentRule; }
            set { _indentRule = value; }
        }

        private Dictionary<string, string> _loggingQueue;

        /// <summary>
        /// Instanciates a new instance of "Log". Throws "FileNotFoundException" if the provided Logfile does not exist.
        /// </summary>
        /// <param name="logFile"></param>
        /// <param name="rule"></param>
        /// <exception cref="FileNotFoundException"></exception>
        public Log(string logFile, IndentRules rule)
        {
            if (!ValidateLogFile(logFile)) { throw new FileNotFoundException(logFile); }
            _logFile = logFile;
            _indentRule = rule;
            _loggingQueue = new();
        }

        /// <summary>
        /// Write the specified message in relation to the messageSource to the Logfile.
        /// <list type="bullet">
        ///     <item>0 = Info</item>
        ///     <item>1 = Minor</item>
        ///     <item>2 = Major</item>
        ///     <item>3 = Critical</item>
        /// </list>
        /// </summary>
        ///     <param name="messageSource"></param>
        ///     <param name="message"></param>
        /// <returns>true if success, false if not</returns>
        public bool TryWriteToLog(string messageSource, string message, int criticality, string? details)
        {
            if (!IsReadyToWrite()) { return false; }

            var time = GetTime();

            string logText = string.Empty; 
            string crit = GetCriticalityString(criticality);

            if (details is null)
            {
                logText = $"[{time}] [{crit}] {messageSource} -> {message};\n";
            }
            else
            {
                logText = $"[{time}] [{crit}] {messageSource} -> {message};\n\tDetails: {details}\n";
            }

            File.WriteAllText(_logFile, logText);

            return true;

        }

        private static DateTime GetTime() { return DateTime.Now; }
        private bool ValidateLogFile(string logFile)
        {
            if (!File.Exists(logFile)) { return false; }

            try
            {
                FileStream test = File.OpenRead(_logFile);
                test.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool IsReadyToWrite()
        {
            if (_logFile == null) { return false; }
            if (!ValidateLogFile(_logFile)) { return false; }

            return true;
        }
        private string GetCriticalityString(int c)
        {
            var crit = new Criticality();

            switch(c)
            {
                case 0:
                    return crit.Info;
                case 1:
                    return crit.Minor;
                case 2:
                    return crit.Major;
                case 3:
                    return crit.Critical;
                case > 3:
                    throw new ArgumentOutOfRangeException();
            }

            return string.Empty;
        }
    }
}