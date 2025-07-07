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

from pickle import FALSE
import numpy as np
import cv2
from typing import Sequence
import aria.sdk as aria
from psi_common import *
import time
from aria_pipes import AriaTrackingProcessor

PORTS = {
    "slam1": "tcp://*:5550",
    "slam2": "tcp://*:5551",
    "rgb": "tcp://*:5552",
    "eyes": "tcp://*:5553",
    "accel0": "tcp://*:5554",
    "accel1": "tcp://*:5555",    
    "gyro0": "tcp://*:5556",
    "gyro1": "tcp://*:5557",    
    "magneto": "tcp://*:5558",
    "baro": "tcp://*:5559",
    "audio": "tcp://*:5560",
    "hands": "tcp://*:5561",
    "skeleton": "tcp://*:5562",
    "gaze": "tcp://*:5563",
}

sockets = {}
for topic, port in PORTS.items():
    sockets[topic] = setup_output_socket(port)

from datetime import datetime

def convert_ns_to_psi_ticks(capture_timestamp_ns: int, context) -> int:
    if context.start_time_ticks is None:
        context.start_time_ticks = datetime_to_ticks(datetime.utcnow())
        context.start_time_ns = capture_timestamp_ns

    relative_ns = capture_timestamp_ns - context.start_time_ns
    return context.start_time_ticks + (relative_ns // 100)

class AriaNetMQStreamTransport:
    def __init__(self, visualizer=None):
        self.start_time_ticks = None
        self.start_time_ns = None
        self.visualizer = visualizer
        self.tracker = AriaTrackingProcessor() 
 
    def on_image_received(self, image: np.array, record) -> None:
        camera_id = record.camera_id
        timestamp = convert_ns_to_psi_ticks(record.capture_timestamp_ns, self)
        camera_topic = None

        image_data = {
            "width": image.shape[1],
            "height": image.shape[0],
            "channels": image.shape[2] if len(image.shape) > 2 else 1,
            "image_bytes": None
        }

        if camera_id == aria.CameraId.Rgb:
            camera_topic = "rgb"

            rgb_image = np.rot90(image, -1)
            rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)

            # ✅ Correctly unpack annotated image and tracking results
            annotated_image, tracking_data = self.tracker.process(rgb_image)

            image_data["image_bytes"] = annotated_image.tobytes()

            # === Send hands, skeleton, gaze to their respective ports ===
            if tracking_data.get("hands"):
                send_topic_message(
                    sockets["hands"], "hands",
                    { "values": tracking_data["hands"] },
                    timestamp
                )

            if tracking_data.get("skeleton"):
                send_topic_message(
                    sockets["skeleton"], "skeleton",
                    { "values": tracking_data["skeleton"] },
                    timestamp
                )

            if tracking_data.get("gaze"):
                send_topic_message(
                    sockets["gaze"], "gaze",
                    { "values": tracking_data["gaze"] },
                    timestamp
                )

            if self.visualizer:
                self.visualizer.latest_images[camera_id] = annotated_image

        elif camera_id == aria.CameraId.Slam1:
            camera_topic = "slam1"
            slam_image = np.rot90(image, -1)
            image_data["image_bytes"] = slam_image.tobytes()

            if self.visualizer:
                self.visualizer.latest_images[camera_id] = slam_image

        elif camera_id == aria.CameraId.Slam2:
            camera_topic = "slam2"
            slam_image = np.rot90(image, -1)
            image_data["image_bytes"] = slam_image.tobytes()

            if self.visualizer:
                self.visualizer.latest_images[camera_id] = slam_image

        elif camera_id == aria.CameraId.EyeTrack:
            camera_topic = "eyes"
            image_data["width"] = image.shape[0]
            image_data["height"] = image.shape[1]
            image_data["image_bytes"] = image.tobytes()

            if self.visualizer:
                print("Calling Image Visualisation")
                self.visualizer.latest_images[camera_id] = image

        else:
            raise ValueError(f"Unknown Camera: {camera_id}")

        send_topic_message(sockets[camera_topic], camera_topic, image_data, timestamp, encodeBinary=True)


    def on_imu_received(self, samples: Sequence, imu_idx: int):
        accel_values = []
        gyro_values = []
        timestamp = 0

        for sample in samples:
            timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
            accel_values.append({"sample": np.array(sample.accel_msec2, dtype=np.float32).tolist(), "originatingTime": timestamp})
            gyro_values.append({"sample": np.array(sample.gyro_radsec, dtype=np.float32).tolist(), "originatingTime": timestamp})
        
            # Realtime plot update for each sample
            if self.visualizer and self.visualizer.debug:
                self.visualizer.sensor_plot["accel"][imu_idx].add_samples(
                    sample.capture_timestamp_ns, sample.accel_msec2
                )
                self.visualizer.sensor_plot["gyro"][imu_idx].add_samples(
                    sample.capture_timestamp_ns, sample.gyro_radsec
                )

        if timestamp != 0:
            if imu_idx == 0:
                send_topic_message(sockets["accel0"], "accel0", { "values": accel_values }, timestamp)
                send_topic_message(sockets["gyro0"], "gyro0", { "values": gyro_values }, timestamp)
            elif imu_idx == 1:
                send_topic_message(sockets["accel1"], "accel1", { "values": accel_values }, timestamp)
                send_topic_message(sockets["gyro1"], "gyro1", { "values": gyro_values }, timestamp)
            else:
                raise ValueError(f"Unknown Imu: {imu_idx}")

    def on_magneto_received(self, sample):
        timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
        mag_array = np.array(sample.mag_tesla, dtype=np.float32)
        send_topic_message(sockets["magneto"], "magneto", { "values": mag_array.tolist() }, timestamp)

        # print("Calling on_magneto_received ")
        if self.visualizer and self.visualizer.debug:
            #print("Calling Visuals on_magneto_received ")
            self.visualizer.sensor_plot["magneto"].add_samples(
                sample.capture_timestamp_ns, sample.mag_tesla
            )

    def on_baro_received(self, sample):
        timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
        send_topic_message(sockets["baro"], "baro", { "value": sample.pressure }, timestamp)  

        # Plot in visualizer
        if self.visualizer and self.visualizer.debug:
            self.visualizer.sensor_plot["baro"].add_samples(
                sample.capture_timestamp_ns,
                [sample.pressure]  # Baro expects 1D list
            )

    def on_audio_received(self, audio_and_record, *args):
        if not hasattr(audio_and_record, "data") or audio_and_record.data is None:
            print("Received empty audio data")
            return

        audio_data = np.array(audio_and_record.data, dtype=np.int32)
        if audio_data.size == 0:
            print("Received empty audio data")
            return

        send_topic_message(sockets["audio"], "audio", { "values": audio_data.tobytes() }, encodeBinary=True)

        # Generate timestamp               
        if self.visualizer and self.visualizer.debug:
            norm_audio = audio_data.astype(np.float32) / (np.max(np.abs(audio_data)) + 1e-6)
            timestamp_ns = time.time() * 1e9
            # Downsample to 1 sample (or mean) for the timestamp
            value = np.mean(norm_audio)  # or norm_audio[0]
            self.visualizer.sensor_plot["audio"].add_samples(timestamp_ns, [value])

    def stop(self):
        print("AriaNetMQStreamTransport Stopping stream...")