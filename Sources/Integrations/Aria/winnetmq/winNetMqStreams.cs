
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
            // int width = 1408;  // Replace with your image width
            // int height = 1408; // Replace with your image height
            // int channels = 3; // RGB has 3 channels
                        
            // long imageSize = (long)width * height * channels;

            while (true)
            {
                try
                {

                    // Receive the combined payload
                    byte[] payload = pullSocket.ReceiveFrameBytes();

                    // Validate payload length (minimum required: header + metadata)
                    if (payload.Length < 31) // 7 bytes (header) + 8 bytes (timestamp) + 4 bytes (width) + 4 bytes (height) + 4 bytes (channels) + 4 bytes (StreamType)
                    {
                        Console.WriteLine("Received payload is too small to contain required metadata.");
                        continue;
                    }

                    // ✅ Extract Header (7 bytes)
                    string header = System.Text.Encoding.UTF8.GetString(payload, 0, 7);
                    Console.WriteLine($"Received Header: {header}");

                    // ✅ Extract metadata ensuring Big Endian conversion

                    // Extract timestamp (8 bytes)
                    byte[] timestampBytes = payload.Skip(7).Take(8).ToArray();
                    Array.Reverse(timestampBytes); // Convert from Big Endian
                    long timestamp = BitConverter.ToInt64(timestampBytes, 0);

                    // Extract width (4 bytes)
                    byte[] widthBytes = payload.Skip(15).Take(4).ToArray();
                    Array.Reverse(widthBytes); // Convert from Big Endian
                    int width = BitConverter.ToInt32(widthBytes, 0);

                    // Extract height (4 bytes)
                    byte[] heightBytes = payload.Skip(19).Take(4).ToArray();
                    Array.Reverse(heightBytes); // Convert from Big Endian
                    int height = BitConverter.ToInt32(heightBytes, 0);

                    // Extract channels (4 bytes)
                    byte[] channelsBytes = payload.Skip(23).Take(4).ToArray();
                    Array.Reverse(channelsBytes); // Convert from Big Endian
                    int channels = BitConverter.ToInt32(channelsBytes, 0);

                    // Extract StreamType (4 bytes)
                    byte[] streamTypeBytes = payload.Skip(27).Take(4).ToArray();
                    Array.Reverse(streamTypeBytes); // Convert from Big Endian
                    int StreamType = BitConverter.ToInt32(streamTypeBytes, 0);

                    Console.WriteLine($"Timestamp: {timestamp}, Width: {width}, Height: {height}, Channels: {channels}, StreamType: {StreamType}");

                    // ✅ Extract Image Data
                    int imageDataStartIndex = 31; // 7 (header) + 8 (timestamp) + 4 (width) + 4 (height) + 4 (channels) + 4 (StreamType)
                    int imageDataLength = payload.Length - imageDataStartIndex;
                    byte[] imageBytes = new byte[imageDataLength];
                    Array.Copy(payload, imageDataStartIndex, imageBytes, 0, imageDataLength);

                    // Validate image size
                    if (imageBytes.Length != width * height * channels)
                    {
                        Console.WriteLine($"Unexpected image size: {imageBytes.Length} bytes (expected {width * height * channels} bytes)");
                        continue;
                    }

                    // ✅ Convert byte array to OpenCV image
                    Mat image = new Mat(height, width, MatType.CV_8UC3);
                    Marshal.Copy(imageBytes, 0, image.Data, imageBytes.Length);

                    // ✅ Display the image
                    Cv2.ImShow("KiranM NetMQ Aria Stream", image);
                    Cv2.WaitKey(1); // Allow OpenCV to refresh the display
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
