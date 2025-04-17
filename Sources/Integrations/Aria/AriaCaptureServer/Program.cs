// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AriaCaptureServer
{
    using System;
    using System.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;
    using MessagePack;
    using System.Collections.Generic;
    using System.Dynamic;
    using OpenCvSharp;
    using System.Runtime.InteropServices;
    using System.Collections.Concurrent;
    using System.Threading;

    internal class Program
    {
        static void Main(string[] args)
        {
            using var pipeline = Pipeline.Create();
            var store = PsiStore.Create(pipeline, "AriaStreams", @"D:\Temp\kin");

            var rgbSource = new NetMQSource<dynamic>(
                pipeline,
                "images",
                "tcp://127.0.0.1:5552",
                MessagePackFormat.Instance);

            var slam1Source = new NetMQSource<dynamic>(
                pipeline,
                "slam1",
                "tcp://127.0.0.1:5550",
                MessagePackFormat.Instance);

            var slam2Source = new NetMQSource<dynamic>(
                pipeline,
                "slam2",
                "tcp://127.0.0.1:5551",
                MessagePackFormat.Instance);

            var eyesSource = new NetMQSource<dynamic>(
                pipeline,
                "eyes",
                "tcp://127.0.0.1:5553",
                MessagePackFormat.Instance);

            var audioSource = new NetMQSource<dynamic>(
                pipeline,
                "audio",
                "tcp://127.0.0.1:5560",
                MessagePackFormat.Instance);

            // === Set up concurrent queue and display thread for RGB ===
            var displayQueue = new ConcurrentQueue<byte[]>();

            var displayThread = new Thread(() =>
            {
                while (true)
                {
                    if (displayQueue.TryDequeue(out byte[] imageBytes))
                    {
                        try
                        {
                            using var mat = new Mat(1408, 1408, MatType.CV_8UC3);
                            Marshal.Copy(imageBytes, 0, mat.Data, imageBytes.Length);
                            Cv2.ImShow("RGB Stream", mat);
                            Cv2.WaitKey(1);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"OpenCV display error: {ex.Message}");
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
            displayThread.IsBackground = true;
            displayThread.Start();

            // === RGB Stream Processing ===
            rgbSource.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                // Only queue frame for display if there's space
                if (displayQueue.Count < 2)
                {
                    displayQueue.Enqueue(imageBytes);
                }

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.BGR_24bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("RGB", store);

            // === SLAM 1 Stream ===
            slam1Source.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.Gray_8bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("Slam1", store);

            // === SLAM 2 Stream ===
            slam2Source.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.Gray_8bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("Slam2", store);

            // === Eyes Stream ===
            eyesSource.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.Gray_8bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("Eyes", store);

            // === Audio Stream ===
            var audioFormat = WaveFormat.Create16BitPcm(48000, 2);

            audioSource.Select(iframe =>
            {
                var messageDict = (IDictionary<string, object>)(ExpandoObject)iframe;
                var byteData = (byte[])messageDict["values"];

                return new AudioBuffer(byteData, audioFormat);

            }).Write("Audio", store);

            // === Start pipeline ===
            pipeline.RunAsync();
            Console.WriteLine("Capturing ARIA streams. Press any key to stop recording...");
            Console.ReadKey();
        }
    }
}
