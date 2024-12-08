using System;
using System.IO;

public static class Logger
{
    private static readonly string LogFilePath = "server_log.txt";

    public static void Info(string message)
    {
        WriteLog("INFO", message);
    }

    public static void Warning(string message)
    {
        WriteLog("WARNING", message);
    }

    public static void Error(string message, Exception ex = null)
    {
        WriteLog("ERROR", $"{message}{(ex != null ? ": " + ex.Message : "")}");
    }

    private static void WriteLog(string level, string message)
    {
        var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        Console.WriteLine(logMessage);

        try
        {
            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
        catch (IOException ioEx)
        {
            Console.WriteLine($"Failed to write to log file: {ioEx.Message}");
        }
    }
}
