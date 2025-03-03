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
from collections import deque
from typing import Sequence

import time
import aria.sdk as aria
import numpy as np
from common import ctrl_c_handler
import cv2
import queue
import threading

from projectaria_tools.core.sensor_data import (
    BarometerData,
    ImageDataRecord,
    MotionData,
)

NANOSECOND = 1e-9
import cv2
import numpy as np
from collections import deque
from typing import Sequence

NANOSECOND = 1e-9


import threading

class CVTemporalPlot:
    def __init__(self, title: str, dim: int, window_duration_sec: float = 4, width=500, height=300):
        self.title = title
        self.window_duration = window_duration_sec
        self.timestamps = deque()
        self.samples = [deque() for _ in range(dim)]
        self.width = width
        self.height = height
        self.bg_color = (0, 0, 0)  # Black background
        self.line_colors = [(0, 255, 0), (0, 0, 255), (255, 0, 0)]  # Green, Blue, Red
        self.lock = threading.Lock()  # Add a lock

    def add_samples(self, timestamp_ns: float, samples: Sequence[float]):
        """Safely add new samples to the plot while removing old data outside the time window."""
        with self.lock:  # Protect the deque with a lock
            timestamp = timestamp_ns * NANOSECOND

            # Remove old data outside the window
            while self.timestamps and (timestamp - self.timestamps[0]) > self.window_duration:
                self.timestamps.popleft()
                for sample in self.samples:
                    sample.popleft()

            # Add new data
            self.timestamps.append(timestamp)
            for i, sample in enumerate(samples):
                self.samples[i].append(sample)

    def draw(self):
        """Safely render the sensor data onto an OpenCV window."""
        with self.lock:  # Prevent modification while iterating
            if not self.timestamps:
                return

            img = np.zeros((self.height, self.width, 3), dtype=np.uint8)
            img[:] = self.bg_color  # Fill with background color

            # Normalize timestamps to fit within the plot window
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
    """
    Example KiranM Aria Streams Reader class with OpenCV plotting.
    """

    def __init__(self):
        self.sensor_plot = {
            "accel": [CVTemporalPlot(f"IMU{idx} Accel", 3) for idx in range(2)],
            "gyro": [CVTemporalPlot(f"IMU{idx} Gyro", 3) for idx in range(2)],
            "magneto": CVTemporalPlot("Magnetometer", 3),
            "baro": CVTemporalPlot("Barometer", 1),
            "audio": CVTemporalPlot("Audio Waveform", 1, window_duration_sec=2)  # New audio plot
        }
        self.latest_images = {}  # Store latest images per camera ID
        self.audio_queue = queue.Queue()  # Buffer for audio streaming

    def render_loop(self):
        """
        Continuously refreshes OpenCV windows for images and sensor plots.
        """
        print("Starting stream... Press 'q' to exit.")
        try:
            while True:
                # Render images
                for camera_id, image in list(self.latest_images.items()):
                    if image is not None:
                        cv2.imshow(f"Camera {camera_id}", image)

                # Render sensor plots
                for plots in self.sensor_plot.values():
                    if isinstance(plots, list):  # Multiple IMU sensors
                        for plot in plots:
                            plot.draw()
                    else:
                        plots.draw()

                # Allow user to quit with 'q'
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break

        except KeyboardInterrupt:
            pass
        finally:
            self.stop()

    def stop(self):
        """Closes all OpenCV windows."""
        print("Stopping stream...")
        cv2.destroyAllWindows()


class KinAriaVisualizerStreamingClientObserver:
    """
    Handles incoming sensor data and updates the OpenCV visualizer.
    """

    def __init__(self, visualizer: KinAriaVisualizer):
        self.visualizer = visualizer

    def on_image_received(self, image: np.array, record) -> None:
        """
        Handles image frames from cameras.
        """
        camera_id = record.camera_id
        self.visualizer.latest_images[camera_id] = image

    def on_imu_received(self, samples: Sequence, imu_idx: int) -> None:
        """
        Handles accelerometer and gyroscope data.
        """
        sample = samples[0]
        self.visualizer.sensor_plot["accel"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.accel_msec2)
        self.visualizer.sensor_plot["gyro"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.gyro_radsec)

    def on_magneto_received(self, sample) -> None:
        """
        Handles magnetometer data.
        """
        self.visualizer.sensor_plot["magneto"].add_samples(sample.capture_timestamp_ns, sample.mag_tesla)

    def on_baro_received(self, sample) -> None:
        """
        Handles barometer data.
        """
        self.visualizer.sensor_plot["baro"].add_samples(sample.capture_timestamp_ns, [sample.pressure])

    def on_audio_received(self, audio_and_record, *args) -> None:
        """
        Handles real-time audio streaming from the Aria microphone and plots it.
        """
        if not hasattr(audio_and_record, "data"):
            print("AudioData does not contain 'data' attribute!")
            return

        # Extract raw audio samples
        audio_data = np.array(audio_and_record.data, dtype=np.float32)

        if len(audio_data) == 0:
            print("No audio data received.")
            return

        # Normalize audio data to the range [-1, 1]
        audio_data /= np.max(np.abs(audio_data)) if np.max(np.abs(audio_data)) > 0 else 1

        # Generate timestamps manually for plotting
        timestamp_ns = time.time() * 1e9
        timestamps = np.linspace(timestamp_ns, timestamp_ns + len(audio_data) * 1000, len(audio_data))

        # Add waveform samples
        for t, sample in zip(timestamps, audio_data):
            self.visualizer.sensor_plot["audio"].add_samples(t, [sample])
   
        
    def stop(self):
        print("Stopping stream...")
        self.visualizer.stop()
        cv2.destroyAllWindows()        
