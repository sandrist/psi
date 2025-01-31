
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
using MessagePack;

class winNetMqStreams
{
    static void Main(string[] args)
    {
        using (var pullSocket = new PullSocket(">tcp://127.0.0.1:5560"))
        {
            Console.WriteLine("KiranM NETMQC WSL to Windows Interface");
                        
            while (true)
            {
                try
                {
                    // Receive the packed MessagePack data
                    byte[] receivedData = pullSocket.ReceiveFrameBytes();

                    // Unpack MessagePack data
                    var message = MessagePackSerializer.Deserialize<AriaMessage>(receivedData);

                    // Print received metadata
                    Console.WriteLine($"Received Header: {message.Header}");
                    Console.WriteLine($"Timestamp: {message.Timestamp}");
                    Console.WriteLine($"Width: {message.Width}, Height: {message.Height}, Channels: {message.Channels}");
                    Console.WriteLine($"StreamType: {message.StreamType}");

                    // Ensure data size is valid
                    int expectedSize = message.Width * message.Height * message.Channels;
                    if (message.ImageBytes.Length != expectedSize)
                    {
                        Console.WriteLine($"Warning: Received image size {message.ImageBytes.Length}, expected {expectedSize}");
                        continue;
                    }

                    // Convert raw bytes to OpenCV Mat
                    Mat image = new Mat(message.Height, message.Width, MatType.CV_8UC3);
                    Marshal.Copy(message.ImageBytes, 0, image.Data, message.ImageBytes.Length);

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
       
}
// Define C# class matching the MessagePack structure
[MessagePackObject]
public class AriaMessage
{
    [Key("header")]
    public string Header { get; set; }

    [Key("timestamp")]
    public long Timestamp { get; set; }

    [Key("width")]
    public int Width { get; set; }

    [Key("height")]
    public int Height { get; set; }

    [Key("channels")]
    public int Channels { get; set; }

    [Key("StreamType")]
    public int StreamType { get; set; }

    [Key("image_bytes")]
    public byte[] ImageBytes { get; set; }
}