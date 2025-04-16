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
                        
            // Start Image Processing 
            rgbSource.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.BGR_24bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
                          
                return psiImage;
            }).EncodeJpeg().Write("RGB", store);

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

            slam2Source.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height,width, PixelFormat.Gray_8bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("Slam2", store);

            eyesSource.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(width, height, PixelFormat.Gray_8bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }).EncodeJpeg().Write("Eyes", store);
                        
            // Run pipeline asynchronously
            pipeline.RunAsync();
            Console.WriteLine("Capturing ARIA streams. Press any key to stop recording...");
            Console.ReadKey();
        }
    }
}
