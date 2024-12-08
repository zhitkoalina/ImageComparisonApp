using System;
using System.Diagnostics;
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
            else if (request.Method == "POST" && (request.Path == "/singlethread" || request.Path == "/multithread"))
            {
                var imageProcessor = new ImageProcessor();
                bool isMultiThread = request.Path == "/multithread";

                byte[] referenceImageData = File.ReadAllBytes(ImageProcessor.ReferenceImagePath);
                byte[] uploadedImageData = request.Body;

                Stopwatch stopwatch = Stopwatch.StartNew();
                double[,] similarityMatrix = isMultiThread
                    ? imageProcessor.ProcessImageMultiThread(uploadedImageData)
                    : imageProcessor.ProcessImageSingleThread(uploadedImageData);
                stopwatch.Stop();

                long processingTime = stopwatch.ElapsedMilliseconds;

                var response = HttpResponse.CreateComparisonMatrixResponse(similarityMatrix, processingTime, referenceImageData, uploadedImageData);
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
