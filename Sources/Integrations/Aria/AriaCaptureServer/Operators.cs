// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AriaCaptureServer
{
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Numerics;
    using System.Windows;

    /// <summary>
    /// Stream operators and extension methods for processing Aria data.
    /// </summary>
    public static class Operators
    {
        public static IProducer<Vector3> ProcessIMU(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, Vector3>((iframe, _, emitter) =>
            {
                foreach (var value in iframe.values)
                {
                    var x = (float)value["sample"][0];
                    var y = (float)value["sample"][1];
                    var z = (float)value["sample"][2];
                    var timestamp = new DateTime((long)value["originatingTime"]);
                    emitter.Post(new Vector3(x, y, z), timestamp);
                }
            }, deliveryPolicy);

        public static IProducer<Shared<Image>> ProcessImage(this IProducer<dynamic> inputStream, PixelFormat pixelFormat, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Select(iframe =>
            {
                int width = (int)iframe.width;
                int height = (int)iframe.height;
                int channels = (int)iframe.channels;
                byte[] imageBytes = (byte[])iframe.image_bytes;

                var psiImage = ImagePool.GetOrCreate(height, width, pixelFormat);
                psiImage.Resource.CopyFrom(imageBytes, 0, width * height * channels);

                return psiImage;
            }, deliveryPolicy);

        public static IProducer<AudioBuffer> ProcessAudio(this IProducer<dynamic> inputStream, WaveFormat audioFormat, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Select(iframe =>
            {
                var messageDict = (IDictionary<string, object>)(ExpandoObject)iframe;
                var byteData = (byte[])messageDict["values"];
                return new AudioBuffer(byteData, audioFormat);
            }, deliveryPolicy);


        public static IProducer<Point[]> ProcessHands2D(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, Point[]>((iframe, _, emitter) =>
            {
                try
                {
                    DateTime timestamp;
                    try
                    {
                        timestamp = new DateTime((long)iframe.originatingTime);
                    }
                    catch
                    {
                        timestamp = DateTime.UtcNow;
                    }

                    foreach (var handObj in iframe.values)
                    {
                        var points2D = new List<Point>();

                        try
                        {
                            var pointList = (IEnumerable<object>)handObj;

                            foreach (var pt in pointList)
                            {
                                var coords = (IList<object>)pt;
                                double x = Convert.ToDouble(coords[0]);
                                double y = Convert.ToDouble(coords[1]);

                                points2D.Add(new Point(x, y));
                            }

                            emitter.Post(points2D.ToArray(), timestamp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ProcessHands2D] Hand parse error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessHands2D] Frame error: {ex.Message}");
                }
            }, deliveryPolicy);

        public static IProducer<Point[]> ProcessSkeleton2D(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, Point[]>((iframe, _, emitter) =>
            {
                try
                {
                    var points2D = new List<Point>();

                    foreach (var joint in iframe.values)
                    {
                        var point = (IList<object>)joint;
                        double x = Convert.ToDouble(point[0]);
                        double y = Convert.ToDouble(point[1]);
                        points2D.Add(new Point(x, y));
                    }

                    //KiranM: This needs to be revisited with Sean.. I am not sure
                    // if it is right to do this.. to keep the PSI handling happpy
                    // Try to get timestamp from iframe or fallback

                    DateTime timestamp;
                    try
                    {
                        timestamp = iframe.originatingTime != null ? new DateTime((long)iframe.originatingTime) : DateTime.UtcNow;
                    }
                    catch
                    {
                        timestamp = DateTime.UtcNow;
                    }

                    emitter.Post(points2D.ToArray(), timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessSkeleton2D] Error: {ex.Message}");
                }
            }, deliveryPolicy);

        public static IProducer<Point> ProcessGaze2D(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, Point>((iframe, _, emitter) =>
            {
                try
                {
                    if (iframe?.values is IEnumerable<object> values)
                    {
                        var gazePoint = ((IList<object>)values.FirstOrDefault());
                        if (gazePoint?.Count >= 2)
                        {
                            double x = Convert.ToDouble(gazePoint[0]);
                            double y = Convert.ToDouble(gazePoint[1]);

                            DateTime timestamp;
                            try
                            {
                                timestamp = iframe.originatingTime != null ? new DateTime((long)iframe.originatingTime) : DateTime.UtcNow;
                            }
                            catch
                            {
                                timestamp = DateTime.UtcNow;
                            }

                            emitter.Post(new Point(x, y), timestamp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessGaze2D] Error: {ex.Message}");
                }
            }, deliveryPolicy);

        public static IProducer<List<Vector3>> ProcessHands(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, List<Vector3>>((iframe, _, emitter) =>
            {
                try
                {


                    //KiranM: This needs to be revisited with Sean.. I am not sure
                    // if it is right to do this.. to keep the PSI handling happpy
                    // Try to get timestamp from iframe or fallback

                    DateTime timestamp;
                    try
                    {
                        timestamp = new DateTime((long)iframe.originatingTime);
                    }
                    catch
                    {
                        timestamp = DateTime.UtcNow;
                    }

                    // 
                    // Iterate over each hand
                    foreach (var handObj in iframe.values)
                    {
                        var handPoints = new List<Vector3>();

                        try
                        {
                            var pointList = (IEnumerable<object>)handObj;

                            foreach (var pt in pointList)
                            {
                                var coords = (IList<object>)pt;
                                float x = Convert.ToSingle(coords[0]);
                                float y = Convert.ToSingle(coords[1]);
                                float z = Convert.ToSingle(coords[2]);

                                handPoints.Add(new Vector3(x, y, z));
                            }

                            emitter.Post(handPoints, timestamp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ProcessHands] Hand parse error: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessHands] Frame error: {ex.Message}");
                }
            }, deliveryPolicy);


        public static IProducer<List<Vector3>> ProcessSkeleton(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, List<Vector3>>((iframe, _, emitter) =>
            {
                try
                {
                    var joints = new List<Vector3>();

                    foreach (var joint in iframe.values)
                    {
                        var point = (IList<object>)joint;
                        float x = Convert.ToSingle(point[0]);
                        float y = Convert.ToSingle(point[1]);
                        float z = Convert.ToSingle(point[2]);
                        joints.Add(new Vector3(x, y, z));
                    }

                    //KiranM: This needs to be revisited with Sean.. I am not sure
                    // if it is right to do this.. to keep the PSI handling happpy
                    // Try to get timestamp from iframe or fallback

                    DateTime timestamp;
                    try
                    {
                        timestamp = iframe.originatingTime != null ? new DateTime((long)iframe.originatingTime) : DateTime.UtcNow;
                    }
                    catch
                    {
                        timestamp = DateTime.UtcNow;
                    }

                    emitter.Post(joints, timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessSkeleton] Error: {ex.Message}");
                }
            }, deliveryPolicy);

        public static IProducer<List<(Vector2 EyeCenter, Vector2 IrisCenter, Vector2 GazeVector)>> ProcessGaze(this IProducer<dynamic> inputStream, DeliveryPolicy deliveryPolicy = null) =>
            inputStream.Process<dynamic, List<(Vector2, Vector2, Vector2)>>((iframe, _, emitter) =>
            {
                try
                {
                    var gazeList = new List<(Vector2 EyeCenter, Vector2 IrisCenter, Vector2 GazeVector)>();

                    foreach (var eye in iframe.values) // eye = [eyeCenter, irisCenter, gazeVector]
                    {
                        var eyeList = (IList<object>)eye;

                        var eyeCenterList = (IList<object>)eyeList[0];
                        var irisCenterList = (IList<object>)eyeList[1];
                        var gazeVectorList = (IList<object>)eyeList[2];

                        var eyeCenter = new Vector2(Convert.ToSingle(eyeCenterList[0]), Convert.ToSingle(eyeCenterList[1]));
                        var irisCenter = new Vector2(Convert.ToSingle(irisCenterList[0]), Convert.ToSingle(irisCenterList[1]));
                        var gazeVector = new Vector2(Convert.ToSingle(gazeVectorList[0]), Convert.ToSingle(gazeVectorList[1]));

                        gazeList.Add((eyeCenter, irisCenter, gazeVector));
                    }
                    //KiranM: This needs to be revisited with Sean.. I am not sure
                    // if it is right to do this.. to keep the PSI handling happpy
                    // Try to get timestamp from iframe or fallback

                    DateTime timestamp;
                    try
                    {
                        timestamp = iframe.originatingTime != null ? new DateTime((long)iframe.originatingTime) : DateTime.UtcNow;
                    }
                    catch
                    {
                        timestamp = DateTime.UtcNow;
                    }

                    emitter.Post(gazeList, timestamp);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProcessGaze] Error: {ex.Message}");
                }
            }, deliveryPolicy);
    }
}
