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
import time
import numpy as np
import threading
import wave
import msgpack
import queue
import base64
import json
import zmq
import sys
from collections import deque
from typing import Sequence
import aria.sdk as aria
from projectaria_tools.core.sensor_data import BarometerData, ImageDataRecord, MotionData

NANOSECOND = 1e-9

PORTS = {
    "camera_0": ("tcp://*:5550", "slam1"),
    "camera_1": ("tcp://*:5551", "slam2"),
    "camera_2": ("tcp://*:5552", "images"),
    "camera_3": ("tcp://*:5553", "eyes"),
    "accel0": ("tcp://*:5554", "accel0"),
    "accel1": ("tcp://*:5555", "accel1"),    
    "gyro0": ("tcp://*:5556", "gyro0"),
    "gyro1": ("tcp://*:5557", "gyro1"),    
    "magneto": ("tcp://*:5558", "magneto"),
    "baro": ("tcp://*:5559", "baro"),
    "audio": ("tcp://*:5560", "audio"),
}

context = zmq.Context()
publishers = {}
topic_labels = {}

for key, (port, label) in PORTS.items():
    socket = context.socket(zmq.PUB)
    socket.bind(port)
    publishers[key] = socket
    topic_labels[key] = label

from datetime import datetime

timestamp_lock = threading.Lock()
last_timestamp = 0

def get_utc_timestamp():
    global last_timestamp
    with timestamp_lock:
        new_timestamp = int((datetime.utcnow() - datetime(1, 1, 1)).total_seconds() * 10**7)
        last_timestamp = max(last_timestamp + 1, new_timestamp)
        return last_timestamp

def convert_ns_to_psi_ticks(capture_timestamp_ns: int, context) -> int:
    if context.start_time_ticks is None:
        context.start_time_ticks = get_utc_timestamp()
        context.start_time_ns = capture_timestamp_ns

    relative_ns = capture_timestamp_ns - context.start_time_ns
    return context.start_time_ticks + (relative_ns // 100)


class AriaNetMQStreamTransport:
    def __init__(self):
        self.sample_rate = 48000
        self.start_time_ticks = None
        self.start_time_ns = None    

    def send_on_netmq(self, topic: str, data: dict):
        try:
            if topic in publishers:
                socket = publishers[topic]
                label = topic_labels[topic]
                video_payload = {
                    "message": data,
                    "originatingTime": data.get("originatingTime", get_utc_timestamp())
                }
                socket.send_multipart([label.encode(), msgpack.dumps(video_payload)])
            else:
                print(f"Warning: No publisher found for topic {topic}")
        except Exception as e:
            print(f"Error sending {topic} data: {e}")        

    def on_image_received(self, image: np.array, record) -> None:
        camera_id = record.camera_id
        timestamp = convert_ns_to_psi_ticks(record.capture_timestamp_ns, self)

        image_data = {
            "header": "AriaVMQ",
            "width": image.shape[1],
            "height": image.shape[0],
            "channels": image.shape[2] if len(image.shape) > 2 else 1,
            "StreamType": camera_id,
            "image_bytes": image.tobytes(),
            "originatingTime": timestamp
        }

        if camera_id in {0, 1}:
            slam_image = np.rot90(image, -1)
            image_data["image_bytes"] = slam_image.tobytes()

        self.send_on_netmq(f"camera_{camera_id}", image_data)

    def on_imu_received(self, samples: Sequence, imu_idx: int):
        sample = samples[0]
        timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
        accel_array = np.array(sample.accel_msec2, dtype=np.float32)
        gyro_array = np.array(sample.gyro_radsec, dtype=np.float32)

        if imu_idx == 0:
            self.send_on_netmq("accel0", {"values": accel_array.tolist(), "originatingTime": timestamp})
            self.send_on_netmq("gyro0", {"values": gyro_array.tolist(), "originatingTime": timestamp})
        elif imu_idx == 1:
            self.send_on_netmq("accel1", {"values": accel_array.tolist(), "originatingTime": timestamp})
            self.send_on_netmq("gyro1", {"values": gyro_array.tolist(), "originatingTime": timestamp})
        else:
            raise ValueError(f"Unknown Imu: {imu_idx}")

    def on_magneto_received(self, sample):
        timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
        mag_array = np.array(sample.mag_tesla, dtype=np.float32)
        self.send_on_netmq("magneto", {"values": mag_array.tolist(), "originatingTime": timestamp})  

    def on_baro_received(self, sample):
        timestamp = convert_ns_to_psi_ticks(sample.capture_timestamp_ns, self)
        baro_array = np.array(sample.pressure, dtype=np.float32)
        self.send_on_netmq("baro", {"values": baro_array.tolist(), "originatingTime": timestamp })  

    def on_audio_received(self, audio_and_record, *args):
        if not hasattr(audio_and_record, "data") or audio_and_record.data is None:
            print("Received empty audio data")
            return

        audio_data = np.array(audio_and_record.data, dtype=np.int32)
        if audio_data.size == 0:
            print("Received empty audio data")
            return

        self.send_on_netmq("audio", {"values": audio_data.tobytes()})

    def stop(self):
        print("AriaNetMQStreamTransport Stopping stream...")