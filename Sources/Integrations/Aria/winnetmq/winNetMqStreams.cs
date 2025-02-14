
using System;
using System.Text;
using System.Linq;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using OpenCvSharp;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Psi;
using Microsoft.Psi.Interop.Format;
using Microsoft.Psi.Interop.Serialization;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Interop.Transport;
using System.Drawing;
using static System.Formats.Asn1.AsnWriter;
using MessagePack;
using System.Net;
using System.ServiceModel.Channels;
using System.Diagnostics;

class winNetMqStreams
{
    static void Main(string[] args)
    {
        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaImages", @"d:/temp/kin");

            var ariaImagesSource = new NetMQSource<dynamic>(
                pipeline,
                "images",
                "tcp://127.0.0.1:5560",
                MessagePackFormat.Instance);

            var ariaSlamSource = new NetMQSource<dynamic>(
                pipeline,
                "slam",
                "tcp://127.0.0.1:5561",
                MessagePackFormat.Instance);

            Mat matImage = new Mat(1408, 1408, MatType.CV_8UC3);
            Mat slamImage = new Mat(640, 480*2, MatType.CV_8UC1);

            var processedStream = ariaImagesSource.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                // Convert raw bytes to OpenCV Mat
                
                var psiImage = ImagePool.GetOrCreate(width, height, PixelFormat.BGR_24bpp);
                                
                // This is for the PsiStore
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
               
                // Convert raw bytes to OpenCV Mat and display it
                Marshal.Copy(imageBytes, 0, matImage.Data, width * height * channels);
                Cv2.ImShow("KiranM Aria Stream", matImage);
                Cv2.WaitKey(1);

                return psiImage;
            });
            
            processedStream.Write("VideoImages", store);

            var processedSlam = ariaSlamSource.Select(sframe =>
            {
                int swidth = (int)sframe.width;
                int sheight = (int)sframe.height;
                int schannels = (int)sframe.channels;
                byte[] slamimageBytes = (byte[])sframe.image_bytes;
                            
                var psiSlam = ImagePool.GetOrCreate( swidth, sheight, PixelFormat.Gray_8bpp);

                // This is for the PsiStore
                psiSlam.Resource.CopyFrom(slamimageBytes, 0, swidth * sheight * schannels);
                                
                Marshal.Copy(slamimageBytes, 0, slamImage.Data, slamimageBytes.Length);
                                
                Cv2.ImShow("KiranM Slam Stream", slamImage);
                Cv2.WaitKey(1);  // Refresh continuously

                return psiSlam;
            });
                                    
            processedSlam.Write("SlamImages", store);
          

            // run the pipeline
            pipeline.RunAsync();

            Console.WriteLine("KiranM: Press any key to fall off the from recording...");
            Console.ReadLine();
        }
    }
}
