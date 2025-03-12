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
using System;
using System.Collections.Generic;

class WinNetMqStreams
{
    static void Main(string[] args)
    {
        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaImages", @"d:/temp/kin");

            var streams = new Dictionary<string, (string Address, PixelFormat Format, Mat Image)>
            {
                { "images", ("tcp://127.0.0.1:5552", PixelFormat.BGR_24bpp, new Mat(1408, 1408, MatType.CV_8UC3)) },
                { "slam1",  ("tcp://127.0.0.1:5550", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "slam2",  ("tcp://127.0.0.1:5551", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "eyes",   ("tcp://127.0.0.1:5553", PixelFormat.Gray_8bpp, new Mat(240, 640, MatType.CV_8UC1)) }
            };

            foreach (var stream in streams)
            {
                string name = stream.Key;
                string address = stream.Value.Address;
                PixelFormat format = stream.Value.Format;
                Mat matImage = stream.Value.Image;

                var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                var processedStream = netMqSource.Select(frame =>
                {
                    int width = (int)frame.width;
                    int height = (int)frame.height;
                    int channels = (int)frame.channels;
                    byte[] imageBytes = (byte[])frame.image_bytes;

                    var psiImage = ImagePool.GetOrCreate(width, height, format);
                    psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                    // Process Image in OpenCV
                    lock (matImage) // Ensure thread safety
                    {
                        Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                        Cv2.ImShow($"NetMQ {name} Stream", matImage);
                        Cv2.WaitKey(1);
                    }

                    return psiImage;
                });

                processedStream.Write($"{name}Images", store);
            }

            // Run pipeline asynchronously
            pipeline.RunAsync();

            Console.WriteLine("KiranM: Press any key to stop recording...");
            Console.ReadLine();
        }
    }
}