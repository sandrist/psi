// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Microsoft.Psi.Interop.Transport
{
    using Microsoft.Psi.Interop.Serialization;

    /// <summary>
    /// NetMQ (ZeroMQ) publisher component.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    public class NetMQAriaReader<T> : NetMQAriaReader, IConsumer<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetMQAriaReader{T}"/> class.
        /// </summary>
        /// <param name="pipeline">The pipeline to add the component to.</param>
        /// <param name="topic">Topic name.</param>
        /// <param name="address">Connection string.</param>
        /// <param name="serializer">Format serializer with which messages are serialized.</param>
        /// <param name="name">An optional name for the component.</param>
        public NetMQAriaReader(Pipeline pipeline, string topic, string address, IFormatSerializer serializer, string name = nameof(NetMQAriaReader<T>))
            : base(pipeline, address, serializer, name)
        {
            this.In = this.AddTopic<T>(topic);
        }

        /// <inheritdoc />
        public Receiver<T> In { get; }
    }
}
