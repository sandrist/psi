
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
using Microsoft.Psi.Interop.Transport;
using System.Drawing;
using static System.Formats.Asn1.AsnWriter;

class winNetMqStreams
{
    static void Main(string[] args)
    {
        using (var pullSocket = new PullSocket(">tcp://127.0.0.1:5560"))
        {
            Console.WriteLine("KiranM NETMQC WSL to Windows Interface");
            // int count = 0;

            // Define fixed dimensions (must match the Python sender)
            int width = 1408;  // Replace with your image width
            int height = 1408; // Replace with your image height
            int channels = 3; // RGB has 3 channels
                        
            long imageSize = (long)width * height * channels;

            while (true)
            {
                                
                try
                {
                    // Receive the combined payload
                    byte[] payload = pullSocket.ReceiveFrameBytes();
                    //Console.WriteLine($"Received payload of size: {payload.Length} bytes");


                    // Validate payload length
                    if (payload.Length < 15) // Minimum expected size: 7 bytes (header) + 8 bytes (timestamp)
                    {
                        Console.WriteLine("Received payload is too small to contain header and timestamp.");
                        continue;
                    }

                    // if (payload.Length != imageSize)
                    // {
                    //     Console.WriteLine($"Unexpected image size: {payload.Length} bytes");
                    //     continue;
                    // }

                    // Extract header (first 7 bytes)
                    string header = Encoding.UTF8.GetString(payload, 0, 7);
                    //Console.WriteLine($"Received Header: {header}");


                    // Extract timestamp (next 8 bytes)
                    long timestamp = BitConverter.ToInt64(payload, 7);


                    // Extract image bytes (remaining data)
                    int imageDataStartIndex = 15; // 7 bytes header + 8 bytes timestamp
                    int imageDataLength = payload.Length - imageDataStartIndex;
                    byte[] imageBytes = new byte[imageDataLength];
                    Array.Copy(payload, imageDataStartIndex, imageBytes, 0, imageDataLength);

                    // Validate image size (optional check if you have a fixed size)
                    if (imageBytes.Length != imageSize)
                    {
                        Console.WriteLine($"Unexpected image size: {imageBytes.Length} bytes");
                        continue;
                    }

                    Mat image = new Mat(height, width, MatType.CV_8UC3);

                    // Copy the raw byte array into the Mat's data buffer
                    Marshal.Copy(payload, 0, image.Data, payload.Length);

                    // Display the image
                    Cv2.ImShow("KiranM NetMQ Aria Stream", image);
                    Cv2.WaitKey(1); // Allow OpenCV to refresh the display

                    // Console.WriteLine($"Received image: Count {count++}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    break;
                }
                
            }
        }
    }

    // Metadata class to deserialize JSON
    public class Metadata
    {
        public int[] Shape { get; set; } = Array.Empty<int>();
        public string Dtype { get; set; } = string.Empty;
    }

}
