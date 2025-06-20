from pickle import FALSE
import time
import cv2
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
import cv2
import mediapipe as mp
import numpy as np

NANOSECOND = 1e-9

from datetime import datetime
import threading


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


class AriaVisualizer:
    def __init__(self, debug: bool = False):
        self.debug = debug
        self.running = False  # Used to control the render loop
        self.sensor_plot = {
            "accel": [CVTemporalPlot(f"IMU{idx} Accel", 3) for idx in range(2)],
            "gyro": [CVTemporalPlot(f"IMU{idx} Gyro", 3) for idx in range(2)],
            "magneto": CVTemporalPlot("Magnetometer", 3),
            "baro": CVTemporalPlot("Barometer", 1),
            "audio": CVTemporalPlot("Audio Waveform", 1, window_duration_sec=2)
        }

        self.latest_images = {}

    def render_loop(self):
        print("Starting stream... Press 'q' in the image window to exit.")
        self.running = True
        cv2.startWindowThread()

        try:
            while self.running:
                if not self.latest_images:
                    print("Waiting for images...")
                    time.sleep(0.5)
                    continue

                for camera_id, image in list(self.latest_images.items()):
                    if image is not None:
                        print(f"Showing image from Camera {camera_id}, shape={image.shape}")
                        display_image = image
                        if len(image.shape) == 2:
                            display_image = cv2.cvtColor(image, cv2.COLOR_GRAY2BGR)
                        cv2.imshow(f"Camera {camera_id}", display_image)

                # Plot sensors
                for plots in self.sensor_plot.values():
                    if isinstance(plots, list):
                        for plot in plots:
                            plot.draw()
                    else:
                        plots.draw()
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
              
        except Exception as e:
            print(f"[render_loop] Exception: {e}")

        finally:
            self.stop()
    

    def stop(self):
        if self.debug:
            print("AriaVisualizer Stopping stream ...")
        self.running = False
        cv2.destroyAllWindows()
