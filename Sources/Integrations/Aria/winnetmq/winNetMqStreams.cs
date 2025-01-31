
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

class winNetMqStreams
{
    static void Main(string[] args)
    {
        //using (var pullSocket = new PullSocket(">tcp://127.0.0.1:5560"))
        using (var pipeline = Pipeline.Create())
        {
            Console.WriteLine("KiranM NETMQC WSL to Windows Interface");

            // receive Aria Streams using the NetMQ Source
            var ariaImagesSource = new NetMQSource<dynamic>(
                    pipeline,
                    "images",
                    "tcp://127.0.0.1:5560",
                    MessagePackFormat.Instance);

            ariaImagesSource.Do(_ => Console.Write('.'));
                                    
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