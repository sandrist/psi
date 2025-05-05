//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
//
namespace AriaCaptureServer
{
    using System;
    using System.Linq;    
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;    
    using System.Collections.Generic;
    using System.Dynamic;    

    internal class Program
    {
        static void Main(string[] args)
        {
            //RunLivePipeline();
            ProcessData();
        }

        static void RunLivePipeline()
        {
            using var pipeline = Pipeline.Create();
            var store = PsiStore.Create(pipeline, "AriaStreams", @"C:\Temp\");

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

                var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.Gray_8bpp);
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

            var audioFormat = WaveFormat.CreatePcm(48000, 32, 7);

            audioSource.Select(iframe =>
            {
                var messageDict = (IDictionary<string, object>)(ExpandoObject)iframe;
                var byteData = (byte[])messageDict["values"];
                return new AudioBuffer(byteData, audioFormat);
            }).Write("Audio", store);

            // Run pipeline asynchronously
            pipeline.RunAsync();
            Console.WriteLine("Capturing ARIA streams. Press any key to stop recording...");
            Console.ReadKey();
        }

        static void ProcessData()
        {
            Console.WriteLine("Processing data...");
            using var pipeline = Pipeline.Create(deliveryPolicy: DeliveryPolicy.Unlimited);

            var outputStore = PsiStore.Create(pipeline, "AriaAudio", @"C:\Temp\");
            var inputStore = PsiStore.Open(pipeline, "AriaStreams", @"C:\Temp\AriaStreams.0022");
            var inputAudio = inputStore.OpenStream<AudioBuffer>("Audio");

            var newAudioFormat = WaveFormat.Create16kHz1Channel16BitPcm();
            inputAudio.Resample(newAudioFormat).Write("ResampledAudio", outputStore);

            pipeline.Run(ReplayDescriptor.ReplayAll);
            Console.WriteLine("Done!");
        }
    }
}