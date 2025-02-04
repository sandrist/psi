
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

        int fwidth = 1408;  // Replace with your image width
        int fheight = 1408; // Replace with your image height
        int fchannels = 3; // RGB has 3 channels

        // Convert raw bytes to OpenCV Mat
        Mat matImage = new Mat(fwidth, fheight, MatType.CV_8UC3);
        var psiImage = ImagePool.GetOrCreate(fwidth, fheight, PixelFormat.BGR_24bpp);

        using (var pipeline = Pipeline.Create())
        {
            var store = PsiStore.Create(pipeline, "AriaImages", @"d:/temp/kin");

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
                
                // This is for the PsiStore
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);
               
                // Convert raw bytes to OpenCV Mat and display it
                Marshal.Copy(imageBytes, 0, matImage.Data, width * height * channels);
                Cv2.ImShow("KiranM Aria Stream", matImage);
                Cv2.WaitKey(1);

                return psiImage;
            });

            //
            // create a store and persist streams
            // 
            
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