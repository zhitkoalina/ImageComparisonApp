using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class HttpRequest
{
    public string Method { get; private set; } = null!;
    public string Path { get; private set; } = null!;
    public byte[] Body { get; private set; } = null!;

    public static string ReadRowHeader(NetworkStream stream) {
        string requestLine = string.Empty;
        string? line;
        string delimiter = "\r\n\r\n";

        using (StreamReader readera = new(stream, Encoding.UTF8, leaveOpen: true)) {
            while ((line = readera.ReadLine()) != null) {
                requestLine += line + "\r\n"; 
                if (requestLine.EndsWith(delimiter)) {
                    requestLine = requestLine.Substring(
                        0, requestLine.Length - delimiter.Length); 
                    break;
                }
            }
        }

        return requestLine;
    }

    public static List<string> ReadHeader(NetworkStream stream) {
        string requestLine = ReadRowHeader(stream);

        string[] first = requestLine.Split(' ');
        List<string> result = new();

        foreach (var item in first) {
            result.AddRange(item.Split("\r\n"));
        }

        return result;
    }

    private static (int, string?) GetMetadata(List<string> requestLine) {
        int contentLength = 0;
        string? boundary = null;

        int index = requestLine.IndexOf("multipart/form-data;");
        if (index > 1) {
          boundary = requestLine[index+1][9..];
          Logger.Info($"Boundary found: {boundary}");
        }
        
        index = requestLine.IndexOf("Content-Length:");
        if (index > 1) {
          contentLength = int.Parse(requestLine[index+1]);
          Logger.Info($"Content-Length found: {contentLength}");
        }

        return (contentLength, boundary);
    }

    public static HttpRequest Parse(Socket clientSocket)
    {
        Logger.Info("Starting to parse HTTP request.");

        using var networkStream = new NetworkStream(clientSocket);
        var requestLine = ReadHeader(networkStream);

        string method = requestLine[0];
        string path = requestLine[1];
        Logger.Info($"Parsed request line: Method = {method}, Path = {path}");

        if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("Request for favicon.ico received. Skipping further processing.");
            return new HttpRequest { Method = method, Path = path, Body = Array.Empty<byte>() };
        }

        var (contentLength, boundary) = GetMetadata(requestLine);

        if (method != "POST" || contentLength <= 0 || boundary == null)
        {
            Logger.Warning("POST request missing required content length or boundary for image data.");
            return new HttpRequest { Method = method, Path = path, Body = Array.Empty<byte>() };
        }

        var body = new byte[contentLength];
        int totalBytesRead = 0;

        while (totalBytesRead < contentLength)
        {
            int bytesRead = networkStream.Read(body, totalBytesRead, contentLength - totalBytesRead);
            if (bytesRead == 0)
            {
                Logger.Error("Unexpected end of stream while reading body.");
                throw new Exception("Incomplete request body.");
            }
            totalBytesRead += bytesRead;
        }

        Logger.Info($"Read {totalBytesRead} bytes from request body.");

        int startBoundaryIndex = FindBoundary(body, boundary);
        if (startBoundaryIndex == -1)
        {
            Logger.Error("Start boundary not found in request body.");
            throw new Exception("Start boundary not found.");
        }

        int endBoundaryIndex = FindBoundary(body, boundary, startBoundaryIndex + boundary.Length);
        if (endBoundaryIndex == -1)
        {
            Logger.Error("End boundary not found in request body.");
            throw new Exception("End boundary not found.");
        }

        int startOfFile = FindDoubleCRLF(body, startBoundaryIndex) + 4;
        if (startOfFile < startBoundaryIndex || startOfFile > endBoundaryIndex)
        {
            Logger.Error("Failed to locate start of file in request body.");
            throw new Exception("Failed to locate start of file in request body.");
        }

        int imageDataLength = endBoundaryIndex - startOfFile;
        var imageData = new byte[imageDataLength];
        Array.Copy(body, startOfFile, imageData, 0, imageDataLength);

        Logger.Info($"Image data extracted successfully. Length: {imageData.Length}");

        return new HttpRequest { Method = method, Path = path, Body = imageData };
    }

    private static int FindBoundary(byte[] body, string boundary, int startIndex = 0)
    {
        byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);
        for (int i = startIndex; i <= body.Length - boundaryBytes.Length; i++)
        {
            if (body.AsSpan(i, boundaryBytes.Length).SequenceEqual(boundaryBytes))
                return i;
        }
        return -1;
    }

    private static int FindDoubleCRLF(byte[] body, int startIndex)
    {
        for (int i = startIndex; i <= body.Length - 4; i++)
        {
            if (body[i] == '\r' && body[i + 1] == '\n' && body[i + 2] == '\r' && body[i + 3] == '\n')
                return i;
        }
        return -1;
    }
}