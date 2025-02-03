
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
using System.Net;
using System.ServiceModel.Channels;


class winNetMqStreams
{
    static void Main(string[] args)
    {
        // Convert raw bytes to OpenCV Mat
        Mat image = new Mat(1408, 1408, MatType.CV_8UC3);
        using (var pipeline = Pipeline.Create())
        {
            Console.WriteLine("KiranM NETMQC WSL to Windows Interface");
            // receive Aria Streams using the NetMQ Source
            var ariaImagesSource = new NetMQSource<dynamic>(
                    pipeline,
                    "images",
                    "tcp://127.0.0.1:5560",
                    MessagePackFormat.Instance);

                // ✅ Use `.Select` to transform the received dynamic object into an `AriaMessage`
            var parsedStream = ariaImagesSource.Select(frame =>
                new AriaMessage
                {
                    Header = frame.header,
                    Width = (int)frame.width,
                    Height = (int)frame.height,
                    Channels = (int)frame.channels,
                    StreamType = (int)frame.StreamType,
                    ImageBytes = (byte[])frame.image_bytes,
                    Timestamp = (long)frame.originatingTime
                });

            // ✅ Subscribe to the processed stream and display the image
            parsedStream.Do(ariaMessage =>
            {
                Console.WriteLine($"Received Image - Timestamp: {ariaMessage.Timestamp}, Size: {ariaMessage.ImageBytes.Length} bytes");

                // Convert raw bytes to OpenCV Mat and display it
                Marshal.Copy(ariaMessage.ImageBytes, 0, image.Data, ariaMessage.Width * ariaMessage.Height * ariaMessage.Channels);
                Cv2.ImShow("KiranM Aria Stream", image);
                Cv2.WaitKey(1);
            });

            pipeline.Run();
        }
    }
}

// Define C# class matching the MessagePack structure
[MessagePackObject]
public class AriaMessage
{
    [Key("header")]
    public string Header { get; set; }
            
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

    [Key("originatingTime")]
    public long Timestamp { get; set; }

}