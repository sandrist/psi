using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Psi;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Interop.Format;
using Microsoft.Psi.Interop.Transport;
using NetMQ;
using Newtonsoft.Json;
using OpenCvSharp;
using MessagePack;

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

            foreach (var stream in streams)
            {
                string name = stream.Key;
                string address = stream.Value.Address;
                PixelFormat format = stream.Value.Format;
                Mat matImage = stream.Value.Image;
                if (name == "slam1" || name == "slam2" || name == "images" || name == "eyes")
                {

                    var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                    var processedStream = netMqSource.Select(frame =>
                    {
                        if (cts.Token.IsCancellationRequested)
                            return null;
                        byte[] imageBytes = (byte[])frame.image_bytes;
                        int width = (int)frame.width;
                        int height = (int)frame.height;
                        int channels = (int)frame.channels;
                        var psiImage = ImagePool.GetOrCreate(height, width, format);
                        psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                        lock (matImage)
                        {
                            Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                            Cv2.ImShow($"NetMQ {name} Stream", matImage);
                            Cv2.WaitKey(1);
                        }
                        return psiImage;
                    });
                    processedStream.Write(name, store);
                }
                else
                {
                    var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                    var processedStream = netMqSource.Select(frame =>
                    {
                        if (cts.Token.IsCancellationRequested)
                            return null;

                        // If frame is a dynamic ExpandoObject, we access its properties directly
                        if (frame is ExpandoObject expandoMessage)
                        {
                            // Convert to IDictionary for easier access to properties
                            var messageDict = (IDictionary<string, object>)expandoMessage;

                            // Assuming the message has a 'values' key, extract it
                            if (messageDict.ContainsKey("values"))
                            {
                                var imuData = messageDict["values"];

                                // Check if imuData is in the expected format (array or list of objects)
                                if (imuData is object[] rawData)
                                {
                                    // Print the IMU data array directly
                                    //Console.WriteLine("Received IMU data:");
                                    foreach (var value in rawData)
                                    {
                                        Console.WriteLine(value);  // Print each value in the array
                                    }
                                }
                                else if (imuData is List<object> listData)
                                {
                                    Console.WriteLine("Received IMU data (List<object>):");
                                    foreach (var value in listData)
                                    {
                                        Console.WriteLine(value);
                                    }
                                }
                                else if (imuData is int || imuData is float || imuData is double)
                                {
                                    // Convert single value to list
                                    Console.WriteLine("Received single value, converting to list:");
                                    List<object> convertedList = new List<object> { imuData };
                                    Console.WriteLine($"Converted Data: {string.Join(", ", convertedList)}");
                                }
                                else if (imuData is string jsonString)
                                {
                                    Console.WriteLine("Received data as a JSON string, possible serialization issue.");
                                    // Optional: Try parsing it as JSON
                                }
                                else
                                {
                                    // Handle unexpected data format if necessary
                                    Console.WriteLine("The 'values' data is not in the expected format (neither object[] nor List<object>).");
                                }

                            }
                            else
                            {
                                // If the 'values' key is not found
                                Console.WriteLine("'values' key not found in the message.");
                            }
                        }
                        return frame;

                    });
                    processedStream.Write(name, store);
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
                        pipeline.Dispose();
                        break;
                    }
                    Thread.Sleep(100);
                }
            });
            pipeline.WaitAll();
            Console.WriteLine("Pipeline terminated.");
        }
    }
}
