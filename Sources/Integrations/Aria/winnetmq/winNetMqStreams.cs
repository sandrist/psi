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
using NAudio.Wave;
using Microsoft.VisualBasic;

class WinNetMqStreams
{

    private static List<byte> audioBuffer = new List<byte>();  // Store byte data instead of shorts
    private static string outputWavFile = "output_audio.wav";
    private static int sampleRate = 44100;  // 44.1 kHz
    private static int channels = 2;        // Stereo

    private static short[] ConvertBytesToInt16(byte[] byteData)
    {
        if (byteData.Length % 2 != 0)
        {
            Console.WriteLine($"Warning: Audio data size {byteData.Length} is not aligned properly. Skipping last byte.");
            byteData = byteData.Take(byteData.Length - 1).ToArray();
        }

        short[] int16Data = new short[byteData.Length / 2];
        Buffer.BlockCopy(byteData, 0, int16Data, 0, byteData.Length);
        return int16Data;
    }

    private static void ProcessAudio(short[] int16Data)
    {
        if (int16Data.Length == 0) return;

        lock (audioBuffer)
        {
            byte[] byteData = new byte[int16Data.Length * 2];
            Buffer.BlockCopy(int16Data, 0, byteData, 0, byteData.Length);
            audioBuffer.AddRange(byteData);
        }

        Console.WriteLine($"Buffered {int16Data.Length / 2} stereo samples.");
    }

    private static void WriteWavFile()
    {
        lock (audioBuffer)
        {
            if (audioBuffer.Count == 0)
            {
                Console.WriteLine("No audio data to write.");
                return;
            }

            try
            {
                using (var writer = new WaveFileWriter(outputWavFile, new WaveFormat(sampleRate, 16, channels)))
                {
                    writer.Write(audioBuffer.ToArray(), 0, audioBuffer.Count);
                }

                Console.WriteLine($"Audio successfully saved to {outputWavFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing WAV file: {ex.Message}");
            }
        }
    }

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
                else if(name == "audio")
                {
                    var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                    var processedStream = netMqSource.Select(frame =>
                    {
                        if (cts.Token.IsCancellationRequested)
                            return null;

                        if (frame is ExpandoObject expandoMessage)
                        {
                            var messageDict = (IDictionary<string, object>)expandoMessage;

                            if (messageDict.ContainsKey("values"))
                            {
                                var rawAudioData = messageDict["values"];

                                if (rawAudioData is byte[] byteData)
                                {
                                    short[] int16Data = ConvertBytesToInt16(byteData);
                                    ProcessAudio(int16Data);
                                }
                                else
                                {
                                    Console.WriteLine("Unexpected audio data format.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("'values' key not found in the message.");
                            }
                        }
                        return frame;
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
                        WriteWavFile(); // Save to WAV on exit
                        break;
                    }
                    Thread.Sleep(100);
                }
            });
            
            pipeline.WaitAll();
            Console.WriteLine("Pipeline terminated.");
            WriteWavFile(); // Save to WAV on exit
        }
    }
}
