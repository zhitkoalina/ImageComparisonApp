using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class HttpServer
{
    private readonly int port;
    private readonly Socket serverSocket;

    public HttpServer(int port)
    {
        this.port = port;
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public void Start()
    {
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        serverSocket.Listen(10);
        Console.WriteLine($"Server started on port {port}");

        ThreadPool.SetMinThreads(10, 10);

        while (true)
        {
            Socket clientSocket = serverSocket.Accept();
            ThreadPool.QueueUserWorkItem(HandleClient, clientSocket);
        }
    }

    private void HandleClient(object clientObj)
    {
        var clientSocket = (Socket)clientObj;
        try
        {
            var request = HttpRequest.Parse(clientSocket);

            if (request.Method == "GET" && request.Path == "/")
            {
                var response = HttpResponse.CreateUploadFormResponse();
                response.Send(clientSocket);
            }
            else if (request.Method == "POST" && request.Path == "/compare")
            {
                var imageProcessor = new ImageProcessor();

                byte[] referenceImageData = File.ReadAllBytes(ImageProcessor.ReferenceImagePath);

                var (singleThreadMatrix, singleThreadTime, multiThreadMatrix, multiThreadTime) =
                    imageProcessor.ProcessImage(request.Body);

                var response = HttpResponse.CreateComparisonMatrixResponse(
                    singleThreadMatrix, singleThreadTime,
                    multiThreadMatrix, multiThreadTime,
                    referenceImageData, request.Body);

                response.Send(clientSocket);
            }
            else
            {
                Logger.Warning($"Unhandled request: {request.Method} {request.Path}");
                var response = HttpResponse.CreateTextResponse("404 Not Found", "404 Not Found");
                response.Send(clientSocket);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error: {ex.Message}");
            var errorResponse = HttpResponse.CreateTextResponse("500 Internal Server Error", ex.Message);
            errorResponse.Send(clientSocket);
        }
        finally
        {
            clientSocket.Close();
        }
    }
}
