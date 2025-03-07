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

import time
import cv2
import numpy as np
import threading
import queue
import json
import zmq
from collections import deque
from typing import Sequence
import aria.sdk as aria
from projectaria_tools.core.sensor_data import BarometerData, ImageDataRecord, MotionData

NANOSECOND = 1e-9
PORTS = {
    "camera_0": "tcp://*:5550",
    "camera_1": "tcp://*:5551",
    "camera_2": "tcp://*:5552",
    "camera_3": "tcp://*:5553",
    "imu": "tcp://*:5560",
    "magneto": "tcp://*:5561",
    "baro": "tcp://*:5562",
    "audio": "tcp://*:5563",
}
context = zmq.Context()
publishers = {key: context.socket(zmq.PUB) for key in PORTS}
for key, socket in publishers.items():
    socket.bind(PORTS[key])

class CVTemporalPlot:
    def __init__(self, title: str, dim: int, window_duration_sec: float = 4, width=500, height=300):
        self.title = title
        self.window_duration = window_duration_sec
        self.timestamps = deque()
        self.samples = [deque() for _ in range(dim)]
        self.width = width
        self.height = height
        self.bg_color = (0, 0, 0)
        self.line_colors = [(0, 255, 0), (0, 0, 255), (255, 0, 0)]
        self.lock = threading.Lock()
    
    def add_samples(self, timestamp_ns: float, samples: Sequence[float]):
        with self.lock:
            timestamp = timestamp_ns * NANOSECOND
            self.timestamps.append(timestamp)
            for i, sample in enumerate(samples):
                self.samples[i].append(sample)
            while self.timestamps and (timestamp - self.timestamps[0]) > self.window_duration:
                self.timestamps.popleft()
                for sample in self.samples:
                    sample.popleft()
    
    def draw(self):
        with self.lock:
            if not self.timestamps:
                return
            img = np.zeros((self.height, self.width, 3), dtype=np.uint8)
            img[:] = self.bg_color
            min_time = self.timestamps[0]
            max_time = self.timestamps[-1]
            time_range = max_time - min_time if max_time > min_time else 1
            for i, sample_series in enumerate(self.samples):
                if len(sample_series) < 2:
                    continue
                normalized_x = [
                    int((t - min_time) / time_range * (self.width - 20)) + 10
                    for t in self.timestamps
                ]
                min_val, max_val = min(sample_series), max(sample_series)
                value_range = max_val - min_val if max_val > min_val else 1
                normalized_y = [
                    self.height - int((s - min_val) / value_range * (self.height - 20)) - 10
                    for s in sample_series
                ]
                for j in range(1, len(normalized_x)):
                    cv2.line(img, (normalized_x[j - 1], normalized_y[j - 1]),
                             (normalized_x[j], normalized_y[j]), self.line_colors[i % len(self.line_colors)], 2)
            cv2.imshow(self.title, img)

class KinAriaVisualizer:
    def __init__(self):
        self.sensor_plot = {
            "accel": [CVTemporalPlot(f"IMU{idx} Accel", 3) for idx in range(2)],
            "gyro": [CVTemporalPlot(f"IMU{idx} Gyro", 3) for idx in range(2)],
            "magneto": CVTemporalPlot("Magnetometer", 3),
            "baro": CVTemporalPlot("Barometer", 1),
            "audio": CVTemporalPlot("Audio Waveform", 1, window_duration_sec=2)
        }
        self.latest_images = {}
    
    def render_loop(self):
        print("Starting stream... Press 'q' to exit.")
        try:
            while True:
                for camera_id, image in list(self.latest_images.items()):
                    if image is not None:
                        cv2.imshow(f"Camera {camera_id}", image)
                for plots in self.sensor_plot.values():
                    if isinstance(plots, list):
                        for plot in plots:
                            plot.draw()
                    else:
                        plots.draw()
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
        except KeyboardInterrupt:
            pass
        finally:
            self.stop()
    
    def stop(self):
        print("Stopping stream...")
        cv2.destroyAllWindows()

class KinAriaStreamingClientObserver:
    def __init__(self, visualizer: KinAriaVisualizer):
        self.visualizer = visualizer
    
    def send_data(self, topic: str, data: dict):
        try:
            publishers[topic].send_json(data)
        except Exception as e:
            print(f"Error sending {topic} data: {e}")
    
    def on_imu_received(self, samples: Sequence, imu_idx: int):
        sample = samples[0]
        imu_data = {"timestamp": sample.capture_timestamp_ns, "accel": sample.accel_msec2, "gyro": sample.gyro_radsec}
        self.visualizer.sensor_plot["accel"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.accel_msec2)
        self.visualizer.sensor_plot["gyro"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.gyro_radsec)
        self.send_data("imu", imu_data)
    
    def on_magneto_received(self, sample):
        magneto_data = {"timestamp": sample.capture_timestamp_ns, "magnetometer": sample.mag_tesla}
        self.visualizer.sensor_plot["magneto"].add_samples(sample.capture_timestamp_ns, sample.mag_tesla)
        self.send_data("magneto", magneto_data)
    
    def on_baro_received(self, sample):
        baro_data = {"timestamp": sample.capture_timestamp_ns, "pressure": sample.pressure}
        self.visualizer.sensor_plot["baro"].add_samples(sample.capture_timestamp_ns, [sample.pressure])
        self.send_data("baro", baro_data)
    
    def on_audio_received(self, audio_and_record, *args):
        audio_data = np.array(audio_and_record.data, dtype=np.float32)
        if len(audio_data) == 0:
            return
        audio_data /= np.max(np.abs(audio_data)) if np.max(np.abs(audio_data)) > 0 else 1
        timestamp_ns = time.time() * 1e9
        self.visualizer.sensor_plot["audio"].add_samples(timestamp_ns, [audio_data[0]])
        self.send_data("audio", {"timestamp": timestamp_ns, "audio": audio_data.tolist()})
    
    def stop(self):
        print("Stopping stream...")
        self.visualizer.stop()
