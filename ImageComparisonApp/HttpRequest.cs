using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class HttpRequest
{
    public string Method { get; private set; }
    public string Path { get; private set; }
    public byte[] Body { get; private set; }

    public static HttpRequest Parse(Socket clientSocket)
    {
        Logger.Info("Starting to parse HTTP request.");

        using var networkStream = new NetworkStream(clientSocket);
        using var reader = new StreamReader(networkStream, Encoding.UTF8, leaveOpen: true);

        var requestLine = reader.ReadLine()?.Split(' ');
        if (requestLine == null || requestLine.Length < 3)
        {
            Logger.Error("Invalid request line: request line is null or incomplete.");
            throw new Exception("Invalid request line");
        }

        string method = requestLine[0];
        string path = requestLine[1];
        Logger.Info($"Parsed request line: Method = {method}, Path = {path}");

        if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("Request for favicon.ico received. Skipping further processing.");
            return new HttpRequest { Method = method, Path = path, Body = Array.Empty<byte>() };
        }

        string line;
        int contentLength = 0;
        string boundary = null;

        while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(line.Substring(15).Trim(), out int parsedContentLength))
                {
                    contentLength = parsedContentLength;
                    Logger.Info($"Content-Length found: {contentLength}");
                }
                else
                {
                    Logger.Error("Invalid Content-Length header.");
                    throw new Exception("Invalid Content-Length header.");
                }
            }
            else if (line.StartsWith("Content-Type: multipart/form-data; boundary=", StringComparison.OrdinalIgnoreCase))
            {
                boundary = "--" + line.Substring(line.IndexOf("boundary=") + 9).Trim();
                Logger.Info($"Boundary found: {boundary}");
            }
        }

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