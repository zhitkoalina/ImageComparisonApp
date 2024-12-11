using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ImageProcessorClient
{
    internal class Program
    {
        private const int NumberOfTests = 10;
        private static readonly string[] TestImagePaths = {
            @"D:..\..\..\reference-squares.jpg",
            @"D:..\..\..\reference-squares-4k.jpg",
            @"D:..\..\..\reference-squares-8k.jpg"
        };

        private const string ServerUrl = "http://localhost:8080";

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting client-side load tests...");

            foreach (var imagePath in TestImagePaths)
            {
                Console.WriteLine($"\nTesting image: {Path.GetFileName(imagePath)}");

                double singleThreadTime = await MeasureUploadTime(imagePath, "/singlethread");
                double multiThreadTime = await MeasureUploadTime(imagePath, "/multithread");

                Console.WriteLine($"Single Thread Average Time: {singleThreadTime:F2} ms");
                Console.WriteLine($"Multi-Thread Average Time: {multiThreadTime:F2} ms");
            }
        }

        private static async Task<double> MeasureUploadTime(string imagePath, string endpoint)
        {
            double totalTime = 0;

            for (int i = 0; i < NumberOfTests; i++)
            {
                using var httpClient = new HttpClient();
                using var multipartFormContent = new MultipartFormDataContent();

                try
                {
                    var fileStreamContent = new StreamContent(File.OpenRead(imagePath))
                    {
                        Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") }
                    };
                    multipartFormContent.Add(fileStreamContent, name: "image", fileName: Path.GetFileName(imagePath));

                    var stopwatch = Stopwatch.StartNew();

                    using var response = await httpClient.PostAsync(ServerUrl + endpoint, multipartFormContent);
                    stopwatch.Stop();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        return double.MaxValue;
                    }

                    Console.WriteLine($"Response for {endpoint}: {await response.Content.ReadAsStringAsync()}");
                    totalTime += stopwatch.Elapsed.TotalMilliseconds;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during upload to {endpoint}: {ex.Message}");
                    return double.MaxValue;
                }
            }

            return totalTime / NumberOfTests;
        }
    }
}