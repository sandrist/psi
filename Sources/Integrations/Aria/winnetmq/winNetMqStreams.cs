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
using System.Dynamic;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

class WinNetMqStreams
{    
    static void Main(string[] args)
    {
        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaImages", @"d:/temp/kin");

            var streams = new Dictionary<string, (string Address, PixelFormat Format, Mat Image)>
            {
                { "slam1",  ("tcp://127.0.0.1:5550", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "slam2",  ("tcp://127.0.0.1:5551", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "images", ("tcp://127.0.0.1:5552", PixelFormat.BGR_24bpp, new Mat(1408, 1408, MatType.CV_8UC3)) },
                { "eyes",   ("tcp://127.0.0.1:5553", PixelFormat.Gray_8bpp, new Mat(240, 640, MatType.CV_8UC1)) },
                { "accel0", ("tcp://127.0.0.1:5554", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "accel1", ("tcp://127.0.0.1:5555", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "gyro0",  ("tcp://127.0.0.1:5556", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "gyro1",  ("tcp://127.0.0.1:5557", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "magneto",("tcp://127.0.0.1:5558", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "baro",   ("tcp://127.0.0.1:5559", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) },
                { "audio",  ("tcp://127.0.0.1:5560", PixelFormat.Gray_8bpp, new Mat(1408, 1408, MatType.CV_8UC1)) }
            };

            // Process only the first 11 streams
            foreach (var stream in streams.Take(11))
            {
                string name = stream.Key;
                string address = stream.Value.Address;
                PixelFormat format = stream.Value.Format;
                Mat matImage = stream.Value.Image;
                int width = 1;
                int height = 1;
                int channels = 1;
                int streamtype = 1;

                var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                var processedStream = netMqSource.Select(frame =>
                {
                    if (name == "slam1" || name == "slam2" || name == "images" || name =="eyes")
                    {
                        // Do something specific for "slam1" and "slam2"
                        // Console.WriteLine($"Processing {name} (special handling for slam1 or slam2)");
                        width = (int)frame.width;
                        height = (int)frame.height;
                        channels = (int)frame.channels;
                        streamtype = (int)frame.StreamType;

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
                    }
                    else if (name == "accel0" || 
                                name == "accel1" || 
                                name == "gyro0"  || 
                                name == "gyro1"  ||
                                name == "magneto" ||
                                name == "baro"    ||
                                name == "audio")
                    {
                        Console.WriteLine(name);   
                        try
                        {
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
                        }
                        catch (Exception ex)
                        {
                            // Catch and log any errors that occur during processing
                            Console.WriteLine($"Error processing frame: {ex.Message}");
                        }

                        return frame; // Return unchanged frame if needed for further processing
                    }
                    else
                    {  return frame; }
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