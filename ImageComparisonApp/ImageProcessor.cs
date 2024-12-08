﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public class ImageProcessor
{
    public const string ReferenceImagePath = "D:\\!учеба\\7 семестр\\!!курсачРИС\\проект\\ImageComparisonApp\\ImageComparisonApp\\reference.jpg";

    public double[,] ProcessImageSingleThread(byte[] imageData)
    {
        using var referenceImage = new Bitmap(ReferenceImagePath);
        using var uploadedImage = new Bitmap(new MemoryStream(imageData));

        var referenceHistograms = CalculateHistogramsSingleThread(referenceImage);
        var uploadedHistograms = CalculateHistogramsSingleThread(uploadedImage);

        return CompareHistograms(referenceHistograms, uploadedHistograms);
    }

    public double[,] ProcessImageMultiThread(byte[] imageData)
    {
        using var referenceImage = new Bitmap(ReferenceImagePath);
        using var uploadedImage = new Bitmap(new MemoryStream(imageData));

        var referenceHistograms = CalculateHistogramsMultiThread(referenceImage);
        var uploadedHistograms = CalculateHistogramsMultiThread(uploadedImage);

        return CompareHistograms(referenceHistograms, uploadedHistograms);
    }

    private List<int[][]> CalculateHistogramsSingleThread(Bitmap image)
    {
        var histograms = new List<int[][]>();
        int fragmentWidth = image.Width / 4;
        int fragmentHeight = image.Height / 4;

        BitmapData bitmapData = image.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.ReadOnly,
            image.PixelFormat);

        int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
        int stride = bitmapData.Stride;

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                int xStart = x * fragmentWidth;
                int yStart = y * fragmentHeight;
                histograms.Add(CalculateFragmentHistogram(bitmapData, xStart, yStart, fragmentWidth, fragmentHeight, stride, bytesPerPixel));
            }
        }

        image.UnlockBits(bitmapData);
        return histograms;
    }

    private List<int[][]> CalculateHistogramsMultiThread(Bitmap image)
    {
        var histograms = new int[16][][];
        int fragmentWidth = image.Width / 4;
        int fragmentHeight = image.Height / 4;

        BitmapData bitmapData = image.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.ReadOnly,
            image.PixelFormat);

        int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
        int stride = bitmapData.Stride;

        var threadPool = new CustomThreadPool(4); // 4 потока

        for (int i = 0; i < 16; i++)
        {
            int taskIndex = i;
            threadPool.EnqueueTask(() =>
            {
                int row = taskIndex / 4;
                int col = taskIndex % 4;
                int xStart = col * fragmentWidth;
                int yStart = row * fragmentHeight;

                histograms[taskIndex] = CalculateFragmentHistogram(bitmapData, xStart, yStart, fragmentWidth, fragmentHeight, stride, bytesPerPixel);
            });
        }

        threadPool.Shutdown();

        image.UnlockBits(bitmapData);
        return histograms.ToList();
    }

    private int[][] CalculateFragmentHistogram(BitmapData bitmapData, int xStart, int yStart, int fragmentWidth, int fragmentHeight, int stride, int bytesPerPixel)
    {
        var redHistogram = new int[256];
        var greenHistogram = new int[256];
        var blueHistogram = new int[256];

        for (int dy = 0; dy < fragmentHeight && (yStart + dy) < bitmapData.Height; dy++)
        {
            for (int dx = 0; dx < fragmentWidth && (xStart + dx) < bitmapData.Width; dx++)
            {
                int pixelOffset = ((yStart + dy) * stride) + ((xStart + dx) * bytesPerPixel);
                byte blue = Marshal.ReadByte(bitmapData.Scan0, pixelOffset);
                byte green = Marshal.ReadByte(bitmapData.Scan0, pixelOffset + 1);
                byte red = Marshal.ReadByte(bitmapData.Scan0, pixelOffset + 2);

                redHistogram[red]++;
                greenHistogram[green]++;
                blueHistogram[blue]++;
            }
        }

        return new[] { redHistogram, greenHistogram, blueHistogram };
    }

    private double[,] CompareHistograms(List<int[][]> referenceHistograms, List<int[][]> uploadedHistograms)
    {
        double[,] similarityMatrix = new double[4, 4];

        for (int i = 0; i < 16; i++)
        {
            int row = i / 4;
            int col = i % 4;
            similarityMatrix[row, col] = CompareSingleFragmentHistograms(referenceHistograms[i], uploadedHistograms[i]);
        }

        return similarityMatrix;
    }

    private double CompareSingleFragmentHistograms(int[][] hist1, int[][] hist2)
    {
        double redDivergence = CompareSingleChannelHistogram(hist1[0], hist2[0]);
        double greenDivergence = CompareSingleChannelHistogram(hist1[1], hist2[1]);
        double blueDivergence = CompareSingleChannelHistogram(hist1[2], hist2[2]);

        return (redDivergence + greenDivergence + blueDivergence) / 3;
    }

    private double CompareSingleChannelHistogram(int[] hist1, int[] hist2)
    {
        double sum1 = hist1.Sum();
        double sum2 = hist2.Sum();
        if (sum1 == 0 || sum2 == 0)
            return double.MaxValue; // если одна из гистограмм пуста, возвращаем максимальное значение

        // Вычисляем KL-дивергенцию
        double klDivergence = 0;
        for (int i = 0; i < hist1.Length; i++)
        {
            double p = hist1[i] / sum1;
            double q = hist2[i] / sum2;

            if (p > 0 && q > 0)
            {
                klDivergence += p * Math.Log(p / q);
            }
        }

        return klDivergence;
    }
}