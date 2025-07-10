// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AriaCaptureServer
{
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using Microsoft.Psi.Interop.Format;
    using Microsoft.Psi.Interop.Transport;
    using System;    
    using System.Numerics;

    internal class Program
    {
        static void Main(string[] args)
        {
            RunLivePipeline();
            //ProcessDataOffline();
        }

        static void RunLivePipeline()
        {
            using var pipeline = Pipeline.Create();
            var store = PsiStore.Create(pipeline, "AriaStreams", @"C:\Temp\");

            // Create a NetMQSource
            NetMQSource<dynamic> CreateSource(string topic, int port) =>
                new (pipeline,
                     topic,
                     $"tcp://127.0.0.1:{port}",
                     MessagePackFormat.Instance);

            // Image sources
            var rgbSource = CreateSource("rgb", 5552);
            var slam1Source = CreateSource("slam1", 5550);
            var slam2Source = CreateSource("slam2", 5551);
            var eyesSource = CreateSource("eyes", 5553);

            // Audio source
            var audioSource = CreateSource("audio", 5560);

            // IMU and other sources
            var accel0Source = CreateSource("accel0", 5554);
            var accel1Source = CreateSource("accel1", 5555);
            var gyro0Source = CreateSource("gyro0", 5556);
            var gyro1Source = CreateSource("gyro1", 5557);
            var magnetoSource = CreateSource("magneto", 5558);
            var baroSource = CreateSource("baro", 5559);

            var handsSource = CreateSource("hands", 5557);
            var skeletonSource = CreateSource("skeleton", 5558);
            var gazeSource = CreateSource("gaze", 5559);

            // Start Image Processing 
            rgbSource.ProcessImage(PixelFormat.BGR_24bpp).EncodeJpeg().Write("RGB", store);
            slam1Source.ProcessImage(PixelFormat.Gray_8bpp).EncodeJpeg().Write("Slam1", store);
            slam2Source.ProcessImage(PixelFormat.Gray_8bpp).EncodeJpeg().Write("Slam2", store);
            eyesSource.ProcessImage(PixelFormat.Gray_8bpp).EncodeJpeg().Write("Eyes", store);

            // Start audio processing
            var audio = audioSource.ProcessAudio(WaveFormat.CreatePcm(48000, 32, 7), DeliveryPolicy.Unlimited);
            audio.Write("Audio", store, deliveryPolicy: DeliveryPolicy.Unlimited);
            audio.Resample(WaveFormat.Create16kHz1Channel16BitPcm(), DeliveryPolicy.Unlimited).Write("ResampledAudio", store, deliveryPolicy: DeliveryPolicy.Unlimited);

            // Process IMU and other data
            accel0Source.ProcessIMU().Write("Accel0", store);
            accel1Source.ProcessIMU().Write("Accel1", store);
            gyro0Source.ProcessIMU().Write("Gyro0", store);
            gyro1Source.ProcessIMU().Write("Gyro1", store);
            magnetoSource.Select(iframe => new Vector3((float)iframe.values[0], (float)iframe.values[1], (float)iframe.values[2])).Write("Magneto", store);
            baroSource.Select(iframe => (double)iframe.value).Write("Baro", store);

            handsSource.ProcessHands().Write("Hands", store);
            skeletonSource.ProcessSkeleton().Write("Skeleton", store);
            gazeSource.ProcessGaze().Write("Gaze", store);


            // Run pipeline asynchronously
            pipeline.RunAsync();
            Console.WriteLine("Capturing ARIA streams. Press any key to stop recording...");
            Console.ReadKey();
        }

        static void ProcessDataOffline()
        {
            // Scratch code for processing data after it has been collected...
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