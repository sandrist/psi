using System.Dynamic;
using System.Runtime.InteropServices;
using LiveCharts;
using LiveCharts.WinForms;
using LiveCharts.Wpf;
using Microsoft.Psi;
using Microsoft.Psi.Imaging;
using Microsoft.Psi.Interop.Format;
using Microsoft.Psi.Interop.Transport;
using OpenCvSharp;
using static System.Net.Mime.MediaTypeNames;

class WinNetMqStreams : Form
{
    private readonly CartesianChart chart;
    private readonly ChartValues<float> accelX, accelY, accelZ;
    private readonly ChartValues<float> gyroX, gyroY, gyroZ;
    private int index = 0;

    public WinNetMqStreams()
    {
        this.Text = "IMU Data Visualization";
        this.Size = new System.Drawing.Size(800, 500);

        chart = new CartesianChart
        {
            Dock = DockStyle.Fill,
            Series = new SeriesCollection
            {
                new LineSeries { Title = "Accel X", Values = (accelX = new ChartValues<float>()) },
                new LineSeries { Title = "Accel Y", Values = (accelY = new ChartValues<float>()) },
                new LineSeries { Title = "Accel Z", Values = (accelZ = new ChartValues<float>()) },
                new LineSeries { Title = "Gyro X", Values = (gyroX = new ChartValues<float>()) },
                new LineSeries { Title = "Gyro Y", Values = (gyroY = new ChartValues<float>()) },
                new LineSeries { Title = "Gyro Z", Values = (gyroZ = new ChartValues<float>()) }
            }
        };

        Controls.Add(chart);
    }

    public void UpdateChart(string name, object[] imuData)
    {
        if (imuData.Length < 3) return; // Ensure at least 3 values (X, Y, Z)

        float x = Convert.ToSingle(imuData[0]);
        float y = Convert.ToSingle(imuData[1]);
        float z = Convert.ToSingle(imuData[2]);

        switch (name)
        {
            case "accel0":
            case "accel1":
                accelX.Add(x);
                accelY.Add(y);
                accelZ.Add(z);
                break;

            case "gyro0":
            case "gyro1":
                gyroX.Add(x);
                gyroY.Add(y);
                gyroZ.Add(z);
                break;
        }

        // Keep chart within 50 points for smooth updates
        if (accelX.Count > 50) accelX.RemoveAt(0);
        if (accelY.Count > 50) accelY.RemoveAt(0);
        if (accelZ.Count > 50) accelZ.RemoveAt(0);
        if (gyroX.Count > 50) gyroX.RemoveAt(0);
        if (gyroY.Count > 50) gyroY.RemoveAt(0);
        if (gyroZ.Count > 50) gyroZ.RemoveAt(0);

        chart.Update();
    }

    public static void Main()
    {
        var imuForm = new WinNetMqStreams();
        Application.Run(imuForm);
    }

    public static void StartPipeline()
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
                { "gyro1",  ("tcp://127.0.0.1:5557", PixelFormat.Gray_8bpp, new Mat(640, 480, MatType.CV_8UC1)) }
            };

            foreach (var stream in streams)
            {
                string name = stream.Key;
                string address = stream.Value.Address;
                Mat matImage = stream.Value.Image;

                var netMqSource = new NetMQSource<dynamic>(pipeline, name, address, MessagePackFormat.Instance);

                var processedStream = netMqSource.Select(frame =>
                {
                    if (name == "slam1" || name == "slam2" || name == "images" || name == "eyes")
                    {
                        byte[] imageBytes = (byte[])frame.image_bytes;
                        lock (matImage)
                        {
                            Marshal.Copy(imageBytes, 0, matImage.Data, imageBytes.Length);
                            Cv2.ImShow($"NetMQ {name} Stream", matImage);
                            Cv2.WaitKey(1);
                        }
                        return frame;
                    }
                    else if (name == "accel0" || name == "accel1" || name == "gyro0" || name == "gyro1")
                    {
                        Console.WriteLine(name);
                        try
                        {
                            if (frame is ExpandoObject expandoMessage)
                            {
                                var messageDict = (IDictionary<string, object>)expandoMessage;

                                if (messageDict.ContainsKey("values") && messageDict["values"] is object[] rawData)
                                {
                                    Console.WriteLine($"Received IMU data ({name}): {string.Join(", ", rawData)}");

                                    // Plot IMU data
                                    Application.OpenForms[0].BeginInvoke((Action)(() =>
                                    {
                                        ((WinNetMqStreams)Application.OpenForms[0]).UpdateChart(name, rawData);
                                    }));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing IMU data: {ex.Message}");
                        }

                        return frame;
                    }
                    return frame;
                });

                processedStream.Write($"{name}Images", store);
            }

            pipeline.RunAsync();

            Console.WriteLine("KiranM: Press any key to stop recording...");
            Console.ReadLine();
        }
    }
}
