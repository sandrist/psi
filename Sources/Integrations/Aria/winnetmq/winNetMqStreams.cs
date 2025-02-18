using System;
using System.Threading;
using System.Threading.Tasks;
using NetMQ;
using Newtonsoft.Json;
using OpenCvSharp;
using System.Runtime.InteropServices;
using Microsoft.Psi;
using Microsoft.Psi.Interop.Format;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Interop.Transport;
using MessagePack;

class WinNetMqStreams
{
    static void Main(string[] args)
    {
        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaImages", @"d:/temp/kin");

            var ImageInstance = MessagePackFormat.Instance;
            var SlamInstance = MessagePackFormat.Instance;

            var ariaImagesSource = new NetMQSource<dynamic>(
                pipeline,
                "images",
                "tcp://127.0.0.1:5560",
                ImageInstance);
                        
            var ariaSlamSource = new NetMQSource<dynamic>(
                pipeline,
                "slam",
                "tcp://127.0.0.1:5561",
                SlamInstance);
            
            Mat matImage = new Mat(1408, 1408, MatType.CV_8UC3);
            Mat slamImage = new Mat(640, 480 * 2, MatType.CV_8UC1);

            // Start Image Processing in a Separate Thread
            Task.Run(() =>
            {
                var processedStream = ariaImagesSource.Select(iframe =>
                {
                    int width = (int)iframe.width;
                    int height = (int)iframe.height;
                    int channels = (int)iframe.channels;
                    byte[] imageBytes = (byte[])iframe.image_bytes;

                    var psiImage = ImagePool.GetOrCreate(width, height, PixelFormat.BGR_24bpp);
                    psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                    // Process Image in OpenCV
                    lock (matImage) // Ensure thread safety
                    {
                        Marshal.Copy(imageBytes, 0, matImage.Data, width * height * channels);
                        Cv2.ImShow("KiranM Aria Stream", matImage);
                        Cv2.WaitKey(1);
                    }

                    return psiImage;
                });

                processedStream.Write("VideoImages", store);
            });

            // Start SLAM Processing in a Separate Thread
            
            Task.Run(() =>
            {
                var processedSlam = ariaSlamSource.Select(sframe =>
                {
                    int swidth = (int)sframe.width;
                    int sheight = (int)sframe.height;
                    int schannels = (int)sframe.channels;
                    byte[] slamimageBytes = (byte[])sframe.image_bytes;

                    var psiSlam = ImagePool.GetOrCreate(swidth, sheight, PixelFormat.Gray_8bpp);
                    psiSlam.Resource.CopyFrom(slamimageBytes, 0, swidth * sheight * schannels);

                    // Process SLAM in OpenCV
                    lock (slamImage) // Ensure thread safety
                    {
                        Marshal.Copy(slamimageBytes, 0, slamImage.Data, slamimageBytes.Length);
                        Cv2.ImShow("KiranM Slam Stream", slamImage);
                        Cv2.WaitKey(1);
                    }

                    return psiSlam;
                });

                processedSlam.Write("SlamImages", store);
            });
            

            // Run pipeline asynchronously
            pipeline.RunAsync();

            Console.WriteLine("KiranM: Press any key to stop recording...");
            Console.ReadLine();
        }
    }
}
