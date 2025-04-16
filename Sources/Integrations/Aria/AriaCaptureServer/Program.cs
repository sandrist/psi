// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AriaCaptureServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Xml.Linq;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;
    using OpenCvSharp;


    internal class Program
    {
        static void Main(string[] args)
        {
            using var pipeline = Pipeline.Create();
            var store = PsiStore.Create(pipeline, "AriaStreams", @"D:\Temp\kin");

            /*
            var audioSource = new NetMQSource<dynamic>(
                pipeline,
                "audio",
                "tcp://127.0.0.1:5560",
                MessagePackFormat.Instance);
            */ 

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

            /* KiranM: Remove Imu for now.. so we can fix the rest of the pipes
            var imuSource = new NetMQSource<dynamic>(
                pipeline,
                "imu",
                "tcp://127.0.0.1:5564",
                MessagePackFormat.Instance,
                useSourceOriginatingTimes: false); //TODO: fix this
            */

            /*
            // Process audio buffers
            var audioFormat = WaveFormat.Create16BitPcm(48000, 2);

            audioSource.Select(iframe =>
            {
                byte[] audioBytes = (byte[])iframe.audio;              
                return new AudioBuffer(audioBytes, audioFormat);
            }).Write("Audio", store);
             
            */ 

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

            /* KiranM: Remove Imu for now.. so we can fix the rest of the pipes
            imuSource.Select(iframe => ((int)iframe.idx, (double)iframe.accel0, (double)iframe.accel1, (double)iframe.accel2, (double)iframe.gyro0, (double)iframe.gyro1, (double)iframe.gyro2))
                .Write("IMU", store);
            */ 

            // Run pipeline asynchronously
            pipeline.RunAsync();
            Console.WriteLine("Capturing ARIA streams. Press any key to stop recording...");
            Console.ReadKey();
        }
    }
}
