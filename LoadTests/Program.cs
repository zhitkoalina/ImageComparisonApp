using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

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

        static void Main(string[] args)
        {
            Console.WriteLine("Starting client-side histogram calculation tests...");

            var imageProcessor = new ImageProcessor();

            foreach (var imagePath in TestImagePaths)
            {
                Console.WriteLine($"\nTesting image: {Path.GetFileName(imagePath)}");

                using var testImage = new Bitmap(imagePath);

                double singleThreadTime = RunHistogramTest(imageProcessor, testImage, isMultithread: false);
                double multiThreadTime = RunHistogramTest(imageProcessor, testImage, isMultithread: true);

                Console.WriteLine($"Single Thread Average Time: {singleThreadTime:F2} ms");
                Console.WriteLine($"Multi-Thread Average Time: {multiThreadTime:F2} ms");
            }

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }

        private static double RunHistogramTest(ImageProcessor imageProcessor, Bitmap testImage, bool isMultithread)
        {
            double totalTime = 0;

            for (int i = 0; i < NumberOfTests; i++)
            {
                var stopwatch = Stopwatch.StartNew();

                var histograms = isMultithread
                    ? imageProcessor.CalculateHistogramsMultiThread(testImage)
                    : imageProcessor.CalculateHistogramsSingleThread(testImage);

                stopwatch.Stop();
                totalTime += stopwatch.Elapsed.TotalMilliseconds;

                Console.WriteLine($"Test {i + 1} - {(isMultithread ? "Multi-Thread" : "Single-Thread")}: Histogram calculated.");
            }

            return totalTime / NumberOfTests;
        }
    }
}