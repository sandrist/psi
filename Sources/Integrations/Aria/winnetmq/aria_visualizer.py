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


from projectaria_tools.core.sensor_data import (
    BarometerData,
    ImageDataRecord,
    MotionData,
)

NANOSECOND = 1e-9

class KinTemporalWindowPlot:
    """
    Manage streaming data, showing the most recent values.
    """

    def __init__(
        self,
        axes,
        title: str,
        dim: int,
        window_duration_sec: float = 4,
    ):
        self.title = title
        self.window_duration = window_duration_sec
        self.timestamps = deque()
        self.samples = [deque() for _ in range(dim)]
        self.count = 0

    def add_samples(self, timestamp_ns: float, samples: Sequence[float]):
        # Convert timestamp to seconds
        timestamp = timestamp_ns * NANOSECOND

        # Remove old data outside of the window
        while (
            self.timestamps and (timestamp - self.timestamps[0]) > self.window_duration
        ):
            self.timestamps.popleft()
            for sample in self.samples:
                sample.popleft()

        # Add new data
        self.timestamps.append(timestamp)
        for i, sample in enumerate(samples):
            self.samples[i].append(sample)

        # Print the new data
        # print(f"[{self.title}] Timestamp: {timestamp:.6f}s, Samples: {samples}")
        # kiranm .. removed .. line next

        # print(f"[{self.title}] Timestamp: {timestamp:.6f}s, Samples: {samples}")


class KinAriaVisualizer:
    """
    Example KiranM Aria Streams Reader class
    """

    def __init__(self):
        self.sensor_plot = {
            "accel": [
                KinTemporalWindowPlot(None, f"IMU{idx} accel", 3)
                for idx in range(2)
            ],
            "gyro": [
                KinTemporalWindowPlot(None, f"IMU{idx} gyro", 3)
                for idx in range(2)
            ],
            "magneto": KinTemporalWindowPlot(None, "Magnetometer", 3),
            "baro": KinTemporalWindowPlot(None, "Barometer", 1),
        }

    def render_loop(self):
        print("Starting stream... Press Ctrl+C to stop.")
        try:
            while True:
                time.sleep(0.1)  # Allow other processes to run and catch interrupts
        except KeyboardInterrupt:
            self.stop()
    
    def stop(self):
        print("Stopping stream...")


class KinBaseStreamingClientObserver:
    """
    Streaming client observer class. Describes all available callbacks that are invoked by the
    streaming client.
    """

    def on_image_received(self, image: np.array, record: ImageDataRecord) -> None:
        pass

    def on_imu_received(self, samples: Sequence[MotionData], imu_idx: int) -> None:
        pass

    def on_magneto_received(self, sample: MotionData) -> None:
        pass

    def on_baro_received(self, sample: BarometerData) -> None:
        pass

    def on_streaming_client_failure(self, reason: aria.ErrorCode, message: str) -> None:
        pass


class KinAriaVisualizerStreamingClientObserver(KinBaseStreamingClientObserver):
    """
    Example implementation of the streaming client observer class.
    Set an instance of this class as the observer of the streaming client using
    set_streaming_client_observer().
    """

    def __init__(self, visualizer: KinAriaVisualizer):
        self.visualizer = visualizer
        self.latest_image = None  # Store the latest image

    def on_image_received(self, image: np.array, record: ImageDataRecord) -> None:
        print(f"[Image] Camera: {record.camera_id}, Shape: {image.shape}")
        
        camera_id = record.camera_id
        height, width, channels = image.shape if len(image.shape) == 3 else (image.shape[0], image.shape[1], 1)

        # Check if it's Camera 2 and has the required shape
        if camera_id == 2 and (height, width, channels) == (1408, 1408, 3):
            print(f"[Image] Camera: {camera_id}, Shape: {image.shape}, Size: {width}x{height}")

            rgb_image = np.rot90(image, -1)
            rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)
            # Display the image
            cv2.imshow(f"Camera {camera_id}", rgb_image)
            cv2.waitKey(1)  # Small delay to refresh window


    def stop(self):
        print("Stopping stream...")
        cv2.destroyAllWindows()  # Close all OpenCV windows when stopping

    def on_imu_received(self, samples: Sequence[MotionData], imu_idx: int) -> None:
        sample = samples[0]
        self.visualizer.sensor_plot["accel"][imu_idx].add_samples(
            sample.capture_timestamp_ns, sample.accel_msec2
        )
        self.visualizer.sensor_plot["gyro"][imu_idx].add_samples(
            sample.capture_timestamp_ns, sample.gyro_radsec
        )

    def on_magneto_received(self, sample: MotionData) -> None:
        self.visualizer.sensor_plot["magneto"].add_samples(
            sample.capture_timestamp_ns, sample.mag_tesla
        )

    def on_baro_received(self, sample: BarometerData) -> None:
        self.visualizer.sensor_plot["baro"].add_samples(
            sample.capture_timestamp_ns, [sample.pressure]
        )

    def on_streaming_client_failure(self, reason: aria.ErrorCode, message: str) -> None:
        print(f"Streaming Client Failure: {reason}: {message}")
