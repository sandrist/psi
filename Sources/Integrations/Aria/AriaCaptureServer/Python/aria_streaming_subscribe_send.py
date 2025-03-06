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
import aria.sdk as aria
import cv2
import numpy as np
from common import quit_keypress, update_iptables
from psi_com import *

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
        "--psi_streams",
        nargs="+",
        choices=["audio", "rgb", "slam"],
        required=True,
        help="Specify the streams to send to psi: audio, rgb, and/or slam.",
    )

    return parser.parse_args()


def main():
    args = parse_args()

     # Print the selected PSI stream type
    print(f"Selected psi streams: {args.psi_streams}")

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

    if "audio" in args.psi_streams:
        print("Sending audio stream to psi...")
    if "rgb" in args.psi_streams:
        print("Sending rgb video stream to psi...")
        rgb_socket = setup_output_socket("tcp://127.0.0.1:5560")
    if "slam" in args.psi_streams:
        print("Sending slam video streams to psi...")
        slam1_socket = setup_output_socket("tcp://127.0.0.1:5561")
        slam2_socket = setup_output_socket("tcp://127.0.0.1:5562")
    
    print("Python sender to Windows pipe is running...")

    # 5. Visualize the streaming data until we close the window
    rgb_window = "Aria RGB"
    slam1_window = "Aria SLAM 1"
    slam2_window = "Aria SLAM 2"
    if "rgb" in args.psi_streams:
        cv2.namedWindow(rgb_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(rgb_window, 1024, 1024)
        cv2.setWindowProperty(rgb_window, cv2.WND_PROP_TOPMOST, 1)
        cv2.moveWindow(rgb_window, 50, 50)

    if "slam" in args.psi_streams:
        cv2.namedWindow(slam1_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(slam1_window, 480 , 640)
        cv2.setWindowProperty(slam1_window, cv2.WND_PROP_TOPMOST, 1)
        cv2.moveWindow(slam1_window, 1100, 50)

        cv2.namedWindow(slam2_window, cv2.WINDOW_NORMAL)
        cv2.resizeWindow(slam2_window, 480 , 640)
        cv2.setWindowProperty(slam2_window, cv2.WND_PROP_TOPMOST, 1)
        cv2.moveWindow(slam2_window, 1100, 50)

    while not quit_keypress():
        # Render the RGB image
        if aria.CameraId.Rgb in observer.images:
            rgb_image = np.rot90(observer.images[aria.CameraId.Rgb], -1)
            rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)

            # ✅ Define fixed parameters (must match C# side)
            width = 1408    # Ensure this is defined BEFORE using it
            height = 1408   # Ensure this is defined BEFORE using it
            channels = 3    # RGB has 3 channels
            StreamType = 6  # Define stream type
                        
            #Define the message structure
            rgb_message = {
                "width": width,             # Image width
                "height": height,            # Image height
                "channels": channels,             # RGB (3 channels)
                "StreamType": StreamType,           # Stream type identifier
                "image_bytes": rgb_image.tobytes(), # Actual image data
            }            

            # Send the serialized data using msgpack
            if "rgb" in args.psi_streams:
                send_topic_message(rgb_socket, "rgb", rgb_message, encodeBinary=True)
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
            schannels = 1    # SLAM has 1 channel
            slamstreamType = 4  # Define stream type                       

            # Allocate a buffer for the stacked grayscale images
            slam1_buffer = np.zeros((sheight, swidth), dtype=np.uint8)  # (height, width) - single channel
            slam2_buffer = np.zeros((sheight, swidth), dtype=np.uint8)  # (height, width) - single channel

            # Copy slam images into the buffer
            slam1_buffer[:, :480] = slam1_image  # Left side
            slam2_buffer[:, :480] = slam2_image  # Right side

            slam1_message = {            
                "width": swidth,             # Image width
                "height": sheight,            # Image height
                "channels": schannels,             # Grayscale (1 channel)
                "StreamType": slamstreamType,          # Stream type identifier
                "image_bytes": slam1_buffer.tobytes(), # Actual image data
            }            

            slam2_message = {       
                "width": swidth,             # Image width
                "height": sheight,            # Image height
                "channels": schannels,             # Grayscale (1 channel)
                "StreamType": slamstreamType,          # Stream type identifier
                "image_bytes": slam2_buffer.tobytes(), # Actual image data
            }            
                                    
            # Send the serialized data using msgpack
            if "slam" in args.psi_streams:
               send_topic_message(slam1_socket, "slam1", slam1_message, encodeBinary=True)
               send_topic_message(slam2_socket, "slam2", slam2_message, encodeBinary=True)
               cv2.imshow(slam1_window, slam1_buffer)
               cv2.imshow(slam2_window, slam2_buffer)
            
            del observer.images[aria.CameraId.Slam1]
            del observer.images[aria.CameraId.Slam2]

    # 6. Unsubscribe to clean up resources
    print("Stop listening to image data")
    streaming_client.unsubscribe()

    # 7. Stop the NetMQ Sockets
    rgb_socket.close()
    slam1_socket.close()
    slam2_socket.close()

    print("KiranM: End of all the processing ")

if __name__ == "__main__":
    main()

