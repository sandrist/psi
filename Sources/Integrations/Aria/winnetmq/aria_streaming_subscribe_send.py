# Copyright (c) Meta Platforms, Inc. and affiliates.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import argparse
import sys
import zmq
import time
import json
import msgpack
import struct
import aria.sdk as aria
import cv2
import numpy as np
from common import quit_keypress, update_iptables

from projectaria_tools.core.sensor_data import ImageDataRecord

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--update_iptables",
        default=False,
        action="store_true",
        help="Update iptables to enable receiving the data stream, only for Linux.",
    )
    parser.add_argument(
        "--psi_stream_type",
        type=str,
        choices=["psi_audio", "psi_video", "psi_slam"],
        required=True,
        help="Specify the PSI reciever stream: audio, video, or slam.",
    )

    return parser.parse_args()


last_timestamp = 0  # Global variable to track the last timestamp

def get_unique_timestamp():
    global last_timestamp
    timestamp = int(time.time() * 1000)  # Current timestamp in milliseconds
    
    # Ensure strictly increasing timestamps
    if timestamp <= last_timestamp:
        timestamp = last_timestamp + 1  # Add 1 millisecond if duplicate

    last_timestamp = timestamp
    return timestamp


def main():
    args = parse_args()
    
     # Print the selected PSI stream type
    print(f"Selected PSI Stream Type: {args.psi_stream_type}")

    if args.update_iptables and sys.platform.startswith("linux"):
        update_iptables()

    #  Optional: Set SDK's log level to Trace or Debug for more verbose logs. Defaults to Info
    aria.set_log_level(aria.Level.Info)

    # 1. Create StreamingClient instance
    streaming_client = aria.StreamingClient()

    #  2. Configure subscription to listen to Aria's RGB and SLAM streams.
    # @see StreamingDataType for the other data types
    config = streaming_client.subscription_config
    config.subscriber_data_type = (
        aria.StreamingDataType.Rgb | aria.StreamingDataType.Slam
    )

    # A shorter queue size may be useful if the processing callback is always slow and you wish to process more recent data
    # For visualizing the images, we only need the most recent frame so set the queue size to 1
    config.message_queue_size[aria.StreamingDataType.Rgb] = 1
    config.message_queue_size[aria.StreamingDataType.Slam] = 1

    # Set the security options
    # @note we need to specify the use of ephemeral certs as this sample app assumes
    # aria-cli was started using the --use-ephemeral-certs flag
    options = aria.StreamingSecurityOptions()
    options.use_ephemeral_certs = True
    config.security_options = options
    streaming_client.subscription_config = config

    # 3. Create and attach observer
    class StreamingClientObserver:
        def __init__(self):
            self.images = {}

        def on_image_received(self, image: np.array, record: ImageDataRecord):
            self.images[record.camera_id] = image

    observer = StreamingClientObserver()
    streaming_client.set_streaming_client_observer(observer)

    # 4. Start listening
    print("Start listening to image data")
    streaming_client.subscribe()

    # KiranM : NetMQ-Protocol
    # 4. Open A NetMQ Socket to Push the data to the Windows side
    print("Start hooking up the NetMQ Interface ")

    if args.psi_stream_type == "psi_audio":
        print("Available PSI Stream Type: psi_audio")
    if args.psi_stream_type == "psi_video":
        print("Available PSI Stream Type: psi_video")
        context = zmq.Context()
        socket = context.socket(zmq.PUB)
        socket.bind("tcp://127.0.0.1:5560")  # Bind to a VIDEO port
    if args.psi_stream_type == "psi_slam":
        print("Available PSI Stream Type: psi_slam")
        slam_context = zmq.Context()
        slam_socket = slam_context.socket(zmq.PUB)
        slam_socket.bind("tcp://127.0.0.1:5561")  # Bind to a SLAM port
    
    print("Python sender to Windows pipe is running...")

    # 5. Visualize the streaming data until we close the window
    rgb_window = "Aria RGB"
    slam_window = "Aria SLAM"
    if args.psi_stream_type == "psi_video":
        cv2.namedWindow(rgb_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(rgb_window, 1024, 1024)
        cv2.setWindowProperty(rgb_window, cv2.WND_PROP_TOPMOST, 1)
        cv2.moveWindow(rgb_window, 50, 50)

    if args.psi_stream_type == "psi_slam":
        cv2.namedWindow(slam_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(slam_window, 480 * 2, 640)
        cv2.setWindowProperty(slam_window, cv2.WND_PROP_TOPMOST, 1)
        cv2.moveWindow(slam_window, 1100, 50)

    while not quit_keypress():
        # Render the RGB image
        if aria.CameraId.Rgb in observer.images:
            rgb_image = np.rot90(observer.images[aria.CameraId.Rgb], -1)
            rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)
                
            # Serialize the image to raw bytes
            image_bytes = rgb_image.tobytes()
            #print(f"Image bytes size: {len(image_bytes)}")
            
            # Get the current timestamp in milliseconds
            # timestamp = int(time.time() * 1000)           
            
            # Get the current timestamp with additional ticks
            timestamp = get_unique_timestamp()

            # Define the string identifier
            header_string = "AriaZMQ"
            header_bytes = header_string.encode('utf-8')  # Convert string to bytes

            # ✅ Define fixed parameters (must match C# side)
            width = 1408    # Ensure this is defined BEFORE using it
            height = 1408   # Ensure this is defined BEFORE using it
            channels = 3    # RGB has 3 channels
            StreamType = 6  # Define stream type
                        
            #Define the message structure
            video_message = {
                "header": "AriaVMQ",       # 7-byte identifier                
                "width": width,             # Image width
                "height": height,            # Image height
                "channels": channels,             # RGB (3 channels)
                "StreamType": StreamType,           # Stream type identifier
                "image_bytes": image_bytes, # Actual image data
                "originatingTime": timestamp      # Milliseconds
            }            
            # Pack the message using MessagePack            
            video_payload = {}
            video_payload[u"message"] = video_message; 
            video_payload[u"originatingTime"] = timestamp ; 

            # Send the serialized data using msgpack
            if args.psi_stream_type == "psi_video":
                socket.send_multipart(["images".encode(), msgpack.dumps(video_payload)])
                cv2.imshow(rgb_window, rgb_image)
            del observer.images[aria.CameraId.Rgb]

        # Stack and display the SLAM images
        if (
            aria.CameraId.Slam1 in observer.images
            and aria.CameraId.Slam2 in observer.images
        ):
            slam1_image = np.rot90(observer.images[aria.CameraId.Slam1], -1)
            slam2_image = np.rot90(observer.images[aria.CameraId.Slam2], -1)
           
            swidth = 480    # Ensure this is defined BEFORE using it
            sheight = 640   # Ensure this is defined BEFORE using it
            schannels = 1    # RGB has 3 channels
            slamstreamType = 4  # Define stream type                       

            # Allocate a buffer for the stacked grayscale images
            slam_buffer = np.zeros((sheight, swidth*2), dtype=np.uint8)  # (height, width) - single channel

            # Copy slam images into the buffer
            slam_buffer[:, :480] = slam1_image  # Left side
            slam_buffer[:, 480:] = slam2_image  # Right side                       
            
            # Serialize the image to raw bytes
            slam_buffer_bytes = slam_buffer.tobytes()

            # Get the current timestamp in milliseconds
            # stimestamp = int(time.time() * 1000)    
            # Get the current timestamp with additional ticks
            stimestamp = get_unique_timestamp()

            slam_message = {
                "header": "AriaSMQ",       # 7-byte identifier                
                "width": swidth*2,             # Image width
                "height": sheight,            # Image height
                "channels": schannels,             # Grayscale (1 channel)
                "StreamType": slamstreamType,          # Stream type identifier
                "image_bytes": slam_buffer_bytes, # Actual image data
                "originatingTime": stimestamp      # Milliseconds
            }            
            # Pack the message using MessagePack            
            slam_payload = {}
            slam_payload[u"message"] = slam_message 
            slam_payload[u"originatingTime"] = stimestamp  
                        
            # Send the serialized data using msgpack
            if args.psi_stream_type == "psi_slam":
               slam_socket.send_multipart(["slam".encode(), msgpack.dumps(slam_payload)])
               cv2.imshow(slam_window, slam_buffer)
            
            del observer.images[aria.CameraId.Slam1]
            del observer.images[aria.CameraId.Slam2]

    # 6. Unsubscribe to clean up resources
    print("Stop listening to image data")
    streaming_client.unsubscribe()

    # 7. Stop the NetMQ Sockets
    socket.close()
    slam_socket.close()
    
    context.term()

    print("KiranM: End of all the processing ")

if __name__ == "__main__":
    main()

