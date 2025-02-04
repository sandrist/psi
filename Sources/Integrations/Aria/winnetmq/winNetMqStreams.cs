
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
using Microsoft.Psi.Imaging;
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
            var ariaImagesSource = new NetMQSource<dynamic>(
                pipeline,
                "images",
                "tcp://127.0.0.1:5560",
                MessagePackFormat.Instance);
                   
            var processedStream = ariaImagesSource.Select(frame =>
            {
                int width = (int)frame.width;
                int height = (int)frame.height;
                int channels = (int)frame.channels;
                byte[] imageBytes = (byte[])frame.image_bytes;

                var psiImage = ImagePool.GetOrCreate(width, height, PixelFormat.BGR_24bpp);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
                            
                return psiImage;

            });

            // create a store and persist streams
            var store = PsiStore.Create(pipeline, "ImagesFromAria", @"d:/temp/kin");
            processedStream.Write("WebcamFrames", store);

            // run the pipeline
            pipeline.RunAsync();

            Console.WriteLine("KiranM: Press any key to fall off the from recording...");
            Console.ReadLine();

        }
    }
}

// Define C# class matching the MessagePack structure
[MessagePackObject]
public class AriaMessage
{
    [Key("header")]
    public string Header { get; set; } = string.Empty;

    [Key("width")]
    public int Width { get; set; }

    [Key("height")]
    public int Height { get; set; }

    [Key("channels")]
    public int Channels { get; set; }

    [Key("StreamType")]
    public int StreamType { get; set; }

    [Key("image_bytes")]
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>(); 

    [Key("originatingTime")]
    public long Timestamp { get; set; }
}