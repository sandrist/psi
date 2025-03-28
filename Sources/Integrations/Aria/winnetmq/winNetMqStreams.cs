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
using System.Collections.Generic;
using System.Dynamic;

class WinNetMqStreams
{
    static void Main(string[] args)
    {
        using (var cts = new CancellationTokenSource())
        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaStreams", @"d:/temp/kin");

            var streams = new Dictionary<string, (string Address, PixelFormat Format, Mat Image)>
            {
                { "slam1",  ("tcp://127.0.0.1:5550", PixelFormat.Gray_8bpp, new Mat(640,480, MatType.CV_8UC1)) },
                { "slam2",  ("tcp://127.0.0.1:5551", PixelFormat.Gray_8bpp, new Mat(640,480, MatType.CV_8UC1)) },
                { "images", ("tcp://127.0.0.1:5552", PixelFormat.BGR_24bpp, new Mat(1408,1408, MatType.CV_8UC3)) },
                { "eyes",   ("tcp://127.0.0.1:5553", PixelFormat.Gray_8bpp, new Mat(240, 640, MatType.CV_8UC1)) },
                { "accel0", ("tcp://127.0.0.1:5554", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "accel1", ("tcp://127.0.0.1:5555", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "gyro0",  ("tcp://127.0.0.1:5556", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "gyro1",  ("tcp://127.0.0.1:5557", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "magneto",("tcp://127.0.0.1:5558", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "baro",   ("tcp://127.0.0.1:5559", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "audio",  ("tcp://127.0.0.1:5560", PixelFormat.Gray_8bpp, new Mat(1408, 1408, MatType.CV_8UC1)) }
            };

            foreach (var stream in streams.Take(11))
            {
                string name = stream.Key;
                string address = stream.Value.Address;
                PixelFormat format = stream.Value.Format;
                Mat matImage = stream.Value.Image;

                var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                if (name == "slam1" || name == "slam2" || name == "images" || name == "eyes")                
                {
                    var processedStream = netMqSource.Select(frame =>
                    {
                        if (cts.Token.IsCancellationRequested)
                            return null; // Stop processing

                            try
                            {
                                byte[] imageBytes = (byte[])frame.image_bytes;
                                int width = (int)frame.width;
                                int height = (int)frame.height;
                                int channels = (int)frame.channels;

                                if (name == "slam1" || name == "slam2" )
                                {
                                    var psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.Gray_8bpp);
                                    psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
                                lock (matImage)
                                {
                                    Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                                    Cv2.ImShow($"NetMQ {name} Stream", matImage);
                                    Cv2.WaitKey(1);
                                }

                                return psiImage;

                            }
                            else if (name == "images")
                                {
                                    var     psiImage = ImagePool.GetOrCreate(height, width, PixelFormat.BGR_24bpp);
                                    psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
                                    lock (matImage)
                                    {
                                        Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                                        Cv2.ImShow($"NetMQ {name} Stream", matImage);
                                        Cv2.WaitKey(1);
                                    }

                                    return psiImage;
                            }
                            else if (name == "eyes")
                                {
                                    var  psiImage = ImagePool.GetOrCreate(width, height, PixelFormat.Gray_8bpp);
                                    psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
                                lock (matImage)
                                {
                                    Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                                    Cv2.ImShow($"NetMQ {name} Stream", matImage);
                                    Cv2.WaitKey(1);
                                }

                                return psiImage;

                            }
                            else
                            {
                                return null; 
                            }



                        }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing {name}: {ex.Message}");
                                return null;
                            }                 
                    });

                    processedStream.Write($"{name}", store);

                }
            }

            pipeline.RunAsync();

            Task.Run(() =>
            {
                Console.WriteLine("Press 'q' to terminate gracefully...");
                while (!cts.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    {
                        cts.Cancel();
                        pipeline.Dispose(); // Properly dispose of the pipeline
                        break;
                    }
                    Thread.Sleep(100); // Prevent CPU overuse
                }
            });

            pipeline.WaitAll();
            Console.WriteLine("Pipeline terminated.");

        }
    }
}