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
from typing import Sequence
from psi_common import *

from projectaria_tools.core.sensor_data import ImageDataRecord

import debugpy

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
        choices=["audio", "rgb", "slam", "eyes", "imu", "magneto", "baro"],
        required=True,
        help="Specify the streams to send to psi: audio, rgb, slam, eyes, imu, magneto, and/or baro.",
    )

    return parser.parse_args()


def main():
    args = parse_args()

     # Print the selected PSI stream types
    print(f"Selected psi streams: {args.psi_streams}")

    if "audio" in args.psi_streams:
        print("Sending audio stream to psi...")
        audio_socket = setup_output_socket("tcp://127.0.0.1:5550")
    if "rgb" in args.psi_streams:
        print("Sending rgb video stream to psi...")
        rgb_socket = setup_output_socket("tcp://127.0.0.1:5560")
    if "slam" in args.psi_streams:
        print("Sending slam video streams to psi...")
        slam1_socket = setup_output_socket("tcp://127.0.0.1:5561")
        slam2_socket = setup_output_socket("tcp://127.0.0.1:5562")
    if "eyes" in args.psi_streams:
        print("Sending eye video streams to psi...")
        eyes_socket = setup_output_socket("tcp://127.0.0.1:5563")
    if "imu" in args.psi_streams:
        print("Sending IMU streams to psi...")
        imu_socket = setup_output_socket("tcp://127.0.0.1:5564")

    if args.update_iptables and sys.platform.startswith("linux"):
        update_iptables()

    #  Optional: Set SDK's log level to Trace or Debug for more verbose logs. Defaults to Info
    # aria.set_log_level(aria.Level.Info)

    # 1. Create StreamingClient instance
    streaming_client = aria.StreamingClient()

    #  2. Configure subscription to listen to Aria's RGB and SLAM streams.
    # @see StreamingDataType for the other data types
    # TODO: This should be set according to the args that were passed in at the top
    config = streaming_client.subscription_config
    config.subscriber_data_type = (
        aria.StreamingDataType.Rgb | aria.StreamingDataType.Slam | aria.StreamingDataType.Audio | aria.StreamingDataType.EyeTrack | aria.StreamingDataType.Imu
    )

    # A shorter queue size may be useful if the processing callback is always slow and you wish to process more recent data
    # For visualizing the images, we only need the most recent frame so set the queue size to 1
    # TODO: Should we change this setting?
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

        def on_imu_received(self, samples: Sequence, imu_idx: int):
            # TODO: Send all values, from all samples, with accurate timestamps per sample
            sample = samples[0]
            imu_message = {
                "accel0": sample.accel_msec2[0],
                "accel1": sample.accel_msec2[1],
                "accel2": sample.accel_msec2[2],
                "gyro0": sample.gyro_radsec[0],
                "gyro1": sample.gyro_radsec[1],
                "gyro2": sample.gyro_radsec[2],
                "idx": imu_idx}
            # debugpy.breakpoint()
            send_topic_message(imu_socket, "imu", imu_message)
        
        def on_audio_received(self, audio_and_record, *args):
            if not hasattr(audio_and_record, "data") or audio_and_record.data is None:
                print("Received empty audio data")
                return
            
            audio_data = np.array(audio_and_record.data, dtype=np.float32)
            if audio_data.size == 0:
                print("Received empty audio data")
                return        

            # debugpy.breakpoint()
            # KiranM: Every Step here is very important and the order of execution.
            # Reshape into 7 channels (assuming the input data is correctly formatted)
            if audio_data.size % 7 == 0:
                audio_data = audio_data.reshape(-1, 7).T  # Transpose to shape (7, N)
            else:
                raise ValueError(f"Unexpected audio data size: {audio_data.size}, cannot reshape to (7, N)")

            if audio_data.shape[0] != 7:
                raise ValueError(f"Expected 7-channel audio input, but got shape {audio_data.shape}")

            # KiranM : Apply a balanced stereo mix:
            # Left: Channels 0, 2, 4 (front-left, left, rear-left) + 50% center
            # Right: Channels 1, 3, 5 (front-right, right, rear-right) + 50% center
            left_mix = (audio_data[0] + audio_data[2] + audio_data[4] + 0.5 * audio_data[6]) / 3
            right_mix = (audio_data[1] + audio_data[3] + audio_data[5] + 0.5 * audio_data[6]) / 3

            # Normalize stereo mix (avoid extreme loudness)
            max_val = max(np.max(np.abs(left_mix)), np.max(np.abs(right_mix)), 1e-8)
            left_mix /= max_val
            right_mix /= max_val

            # Convert to 16-bit PCM format
            stereo_audio = np.vstack((left_mix, right_mix)).T  # Shape (N, 2)
            stereo_audio_int16 = np.int16(stereo_audio * 32767)
            interleaved_audio = stereo_audio_int16.flatten()
            
            audio_message = {
                "audio": interleaved_audio.tobytes(), # audio data
            }

            send_topic_message(audio_socket, "audio", audio_message)

    observer = StreamingClientObserver()
    streaming_client.set_streaming_client_observer(observer)

    # 4. Start listening
    print("Start listening to image data")
    streaming_client.subscribe()

    # # 5. Visualize the streaming data until we close the window
    # rgb_window = "Aria RGB"
    # slam1_window = "Aria SLAM 1"
    # slam2_window = "Aria SLAM 2"
    # if "rgb" in args.psi_streams:
    #     cv2.namedWindow(rgb_window, cv2.WINDOW_NORMAL)
    #     cv2.resizeWindow(rgb_window, 1024, 1024)
    #     cv2.setWindowProperty(rgb_window, cv2.WND_PROP_TOPMOST, 1)
    #     cv2.moveWindow(rgb_window, 50, 50)

    # if "slam" in args.psi_streams:
    #     cv2.namedWindow(slam1_window, cv2.WINDOW_NORMAL)
    #     cv2.resizeWindow(slam1_window, 480 , 640)
    #     cv2.setWindowProperty(slam1_window, cv2.WND_PROP_TOPMOST, 1)
    #     cv2.moveWindow(slam1_window, 1100, 50)

    #     cv2.namedWindow(slam2_window, cv2.WINDOW_NORMAL)
    #     cv2.resizeWindow(slam2_window, 480 , 640)
    #     cv2.setWindowProperty(slam2_window, cv2.WND_PROP_TOPMOST, 1)
    #     cv2.moveWindow(slam2_window, 1100, 50)

    while not quit_keypress():
        # Render the RGB image
        if aria.CameraId.Rgb in observer.images and "rgb" in args.psi_streams:
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
            send_topic_message(rgb_socket, "rgb", rgb_message, encodeBinary=True)
            # cv2.imshow(rgb_window, rgb_image)

            del observer.images[aria.CameraId.Rgb]

        if aria.CameraId.EyeTrack in observer.images and "eyes" in args.psi_streams:
            eyes_image = observer.images[aria.CameraId.EyeTrack]

            eyes_message = {            
                "width": eyes_image.shape[1],   # Width
                "height": eyes_image.shape[0],  # Height
                "channels": 1,             # Grayscale (1 channel)
                "StreamType": 3,          # Stream type identifier
                "image_bytes": eyes_image.tobytes(), # Actual image data
            }

            send_topic_message(eyes_socket, "eyes", eyes_message, encodeBinary=True)

        # Stack and display the SLAM images
        if (aria.CameraId.Slam1 in observer.images
            and aria.CameraId.Slam2 in observer.images
            and "slam" in args.psi_streams
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
            send_topic_message(slam1_socket, "slam1", slam1_message, encodeBinary=True)
            send_topic_message(slam2_socket, "slam2", slam2_message, encodeBinary=True)
            # cv2.imshow(slam1_window, slam1_buffer)
            # cv2.imshow(slam2_window, slam2_buffer)
            
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

