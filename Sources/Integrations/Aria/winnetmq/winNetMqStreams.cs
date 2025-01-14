using System;
using System.Text;
using System.Linq;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json; 
using OpenCvSharp;
using System.Runtime.InteropServices;

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

                    if (payload.Length != imageSize)
                    {
                        Console.WriteLine($"Unexpected image size: {payload.Length} bytes");
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
