using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

public class HttpResponse
{
    public string StatusCode { get; private set; }
    public string ContentType { get; private set; }
    public byte[] Body { get; private set; }

    private HttpResponse() { }

    private static string ReadHtmlFile(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: File not found - {filePath}");
            return "<html><body><h1>File Not Found</h1></body></html>";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return "<html><body><h1>Error Loading Page</h1></body></html>";
        }
    }

    public static HttpResponse CreateTextResponse(string status, string content)
    {
        return new HttpResponse
        {
            StatusCode = status,
            ContentType = "text/plain",
            Body = Encoding.UTF8.GetBytes(content)
        };
    }

    public static HttpResponse CreateHtmlResponse(string content)
    {
        return new HttpResponse
        {
            StatusCode = "200 OK",
            ContentType = "text/html",
            Body = Encoding.UTF8.GetBytes(content)
        };
    }

    public static HttpResponse CreateComparisonMatrixResponse(
        double[,] singleThreadMatrix, long singleThreadTime,
        double[,] multiThreadMatrix, long multiThreadTime,
        byte[] referenceImageData, byte[] uploadedImageData)
    {
        string htmlTemplate = ReadHtmlFile(@"..\\..\\..\\html\\response.html");

        string singleMatrixHtml = ImageProcessor.FormatMatrixAsHtmlTable(singleThreadMatrix);
        string multiMatrixHtml = ImageProcessor.FormatMatrixAsHtmlTable(multiThreadMatrix);

        string referenceImageBase64 = Convert.ToBase64String(referenceImageData);
        string uploadedImageBase64 = Convert.ToBase64String(uploadedImageData);

        string htmlContent = htmlTemplate
            .Replace("{{singleThreadMatrix}}", singleMatrixHtml)
            .Replace("{{multiThreadMatrix}}", multiMatrixHtml)
            .Replace("{{singleThreadTime}}", singleThreadTime.ToString())
            .Replace("{{multiThreadTime}}", multiThreadTime.ToString())
            .Replace("{{referenceImage}}", $"data:image/png;base64,{referenceImageBase64}")
            .Replace("{{uploadedImage}}", $"data:image/png;base64,{uploadedImageBase64}");

        return CreateHtmlResponse(htmlContent);
    }

    public static HttpResponse CreateUploadFormResponse()
    {
        string htmlContent = ReadHtmlFile(@"..\\..\\..\\html\\uploadForm.html");
        return CreateHtmlResponse(htmlContent);
    }

    public void Send(Socket clientSocket)
    {
        try
        {
            string responseHeader = $"HTTP/1.1 {StatusCode}\r\n" +
                                    $"Content-Type: {ContentType}\r\n" +
                                    $"Content-Length: {Body.Length}\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(responseHeader);

            clientSocket.Send(headerBytes);
            clientSocket.Send(Body);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Socket error while sending response: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while sending response: {ex.Message}");
        }
        finally
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during socket shutdown: {ex.Message}");
            }
            finally
            {
                clientSocket.Close();
            }
        }
    }
}
