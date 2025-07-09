// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace AriaCaptureServer
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Numerics;
    using Microsoft.Psi;
    using Microsoft.Psi.Audio;
    using Microsoft.Psi.Imaging;

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

        



    }
}
