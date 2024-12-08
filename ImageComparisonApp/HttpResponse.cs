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
    double[,] matrix, double totalScore, long processingTime, byte[] referenceImageData, byte[] uploadedImageData)
    {
        string htmlTemplate = ReadHtmlFile(@"..\\..\\..\\html\\response.html");

        string matrixHtml = FormatMatrixAsHtmlTable(matrix);

        string referenceImageBase64 = Convert.ToBase64String(referenceImageData);
        string uploadedImageBase64 = Convert.ToBase64String(uploadedImageData);

        string htmlContent = htmlTemplate
            .Replace("{{matrix}}", matrixHtml)
            .Replace("{{totalScore}}", totalScore.ToString())
            .Replace("{{time}}", processingTime.ToString())
            .Replace("{{referenceImage}}", $"data:image/png;base64,{referenceImageBase64}")
            .Replace("{{uploadedImage}}", $"data:image/png;base64,{uploadedImageBase64}");

        return CreateHtmlResponse(htmlContent);
    }

    public static HttpResponse CreateUploadFormResponse()
    {
        string htmlContent = ReadHtmlFile(@"..\\..\\..\\html\\uploadForm.html");
        return CreateHtmlResponse(htmlContent);
    }

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

    private static string FormatMatrixAsHtmlTable(double[,] matrix)
    {
        var sb = new StringBuilder();
        sb.Append("<table border='1'>");
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            sb.Append("<tr>");
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                sb.AppendFormat("<td>{0:F2}</td>", matrix[i, j]);
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }
}
