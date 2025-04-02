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

# Define ports for each data stream with their corresponding labels
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

# Initialize ZeroMQ context and create publishers
context = zmq.Context()
publishers = {}    # Store only sockets
topic_labels = {}  # Separate dictionary for labels

for key, (port, label) in PORTS.items():
    socket = context.socket(zmq.PUB)
    socket.bind(port)
    publishers[key] = socket  # Store only the socket
    topic_labels[key] = label  # Store labels separately

from datetime import datetime
import threading

# Global lock and last timestamp for thread-safe incrementing
timestamp_lock = threading.Lock()
last_timestamp = 0

def get_utc_timestamp():
    global last_timestamp
    with timestamp_lock:
        new_timestamp = int((datetime.utcnow() - datetime(1, 1, 1)).total_seconds() * 10**7)
        # Ensure timestamps always increase
        last_timestamp = max(last_timestamp + 1, new_timestamp)
        return last_timestamp


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
                    else:
                        print(f"No image received for Camera {camera_id}")
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
    
    def __init__(self, visualizer, mode: str):
        """
        :param visualizer: Instance of KinAriaVisualizer
        :param mode: Either "raw" or "processed" to determine image processing mode
        """
        self.visualizer = visualizer
        self.mode = mode  # Determines which processing method to use               
    
    def send_data(self, topic: str, data: dict):
        try:
            publishers[topic].send_json(data)
        except Exception as e:
            print(f"Error sending {topic} data: {e}")
    
    def send_on_netmq(self, topic: str, data: dict):
        """
        Sends structured data over NetMQ using multipart messages.
        """
        try:
            if topic in publishers:
                socket = publishers[topic]  # Correctly get only the socket
                label = topic_labels[topic]  # Retrieve the label from topic_labels dictionary
                video_payload = {
                    "message": data,
                    "originatingTime": data.get("originatingTime", 
                                                get_utc_timestamp())
                }
                socket.send_multipart([label.encode(), msgpack.dumps(video_payload)])
            else:
                print(f"Warning: No publisher found for topic {topic}")
        except Exception as e:
            print(f"Error sending {topic} data: {e}")        

    def on_image_received_raw(self, image: np.array, record) -> None:
        """
        Handles image frames from cameras.
        """
        camera_id = record.camera_id
        self.visualizer.latest_images[camera_id] = image

        # Convert image to a byte array
        img_byte_array = image.tobytes()
        timestamp = get_utc_timestamp()

        # Define structured metadata
        image_data = {
            "header": "AriaVMQ",
            "width": image.shape[1],   # Width
            "height": image.shape[0],  # Height
            "channels": image.shape[2] if len(image.shape) > 2 else 1,  # Handle grayscale images
            "StreamType": camera_id,
            "image_bytes": img_byte_array,
            "originatingTime": timestamp
        }

        # Special handling for Camera 2 (RGB processing)
        if camera_id == 2:
            #print("Processing Camera 2 (RGB)")
            rgb_image = np.rot90(image, -1)
            rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)
            image_data["image_bytes"] = rgb_image.tobytes()
        if camera_id in {0,1}:
            #print("Processing Slam Cameras")
            slam_image = np.rot90(image, -1)            
            image_data["image_bytes"] = slam_image.tobytes()    
        #if camera_id == 3:
            #print("Processing Eyes Cameras")
            #rgb_image = cv2.cvtColor(rgb_image, cv2.COLOR_BGR2RGB)
            # image_data["image_bytes"] = slam_image.tobytes()        
        
        # Send over NetMQ
        self.send_on_netmq(f"camera_{camera_id}", image_data)
        
    def on_image_received_processed(self, image: np.array, record) -> None:
        """
        Handles image frames from cameras.
        """
        camera_id = record.camera_id
        self.visualizer.latest_images[camera_id] = image

        # Try using capture_timestamp_ns or any other time-related field you find
        timestamp_ns = getattr(record, 'capture_timestamp_ns', None)
        if timestamp_ns is None:
            # Handle the case where no timestamp is found (you can use time.time() as a fallback)
            timestamp_ns = time.time() * 1e9

        # Convert the image to a byte array
        _, img_bytes = cv2.imencode('.jpg', image)
        img_byte_array = img_bytes.tobytes()

        # Optionally encode it to base64 if required
        img_base64 = base64.b64encode(img_byte_array).decode('utf-8')

        # Create a dictionary to hold the data and send it using send_data
        image_data = {
            "timestamp": timestamp_ns,
            "camera_id": camera_id,
            "image": img_base64
        }
        self.send_data(f"camera_{camera_id}", image_data)
   
    def on_image_received(self, image: np.array, record) -> None:
        """Determines which function to call based on mode."""
        if self.mode == "raw":
            self.on_image_received_raw(image, record)
        elif self.mode == "processed":
            self.on_image_received_processed(image, record)
        else:
            raise ValueError(f"Unknown mode: {self.mode}")
    
    
    def on_imu_received(self, samples: Sequence, imu_idx: int):
        sample = samples[0]
        imu_data = {"timestamp": sample.capture_timestamp_ns, "accel": sample.accel_msec2, "gyro": sample.gyro_radsec}
        self.visualizer.sensor_plot["accel"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.accel_msec2)
        self.visualizer.sensor_plot["gyro"][imu_idx].add_samples(sample.capture_timestamp_ns, sample.gyro_radsec)
        
        if self.mode == "raw":
                # print(f"Sending Size of accel0: {sys.getsizeof(sample.accel_msec2)} bytes")  # Print size
                # print(f"Sending Size of gyro0: {sys.getsizeof(sample.gyro_radsec)} bytes")  # Print size
                # print(type(sample.accel_msec2), type(sample.gyro_radsec))

                accel_size = sum(sys.getsizeof(v) for v in sample.accel_msec2)
                gyro_size = sum(sys.getsizeof(v) for v in sample.gyro_radsec)

                #print(f"Sending Size of accel0: {accel_size} bytes")
                #print(f"Sending Size of gyro0: {gyro_size} bytes")

                accel_array = np.array(sample.accel_msec2, dtype=np.float32)
                gyro_array = np.array(sample.gyro_radsec, dtype=np.float32)

                #print(f"Accel array size: {accel_array.nbytes} bytes")
                #print(f"Gyro array size: {gyro_array.nbytes} bytes")
                
                if imu_idx == 0:                              
                    self.send_on_netmq("accel0", {"values": accel_array.tolist()})  
                    self.send_on_netmq("gyro0", {"values": gyro_array.tolist()})
                elif imu_idx == 1:
                    #print(f"Size of accel1: {sys.getsizeof(sample.accel_msec2)} bytes")  # Print size
                    #print(f"Size of gyro1: {sys.getsizeof(sample.gyro_radsec)} bytes")  # Print size                                                                   
                    self.send_on_netmq("accel1", {"values": accel_array.tolist()})  
                    self.send_on_netmq("gyro1", {"values": gyro_array.tolist()})
                else:
                    raise ValueError(f"Unknown Imu: {imu_idx}")
        elif self.mode == "processed":
            self.send_data("imu", imu_data)       
    
    def on_magneto_received(self, sample):
        magneto_data = {"timestamp": sample.capture_timestamp_ns, "magnetometer": sample.mag_tesla}
        self.visualizer.sensor_plot["magneto"].add_samples(sample.capture_timestamp_ns, sample.mag_tesla)

        #print(f"Size of magneto_data: {sys.getsizeof(magneto_data)} bytes")  # Print size
        #print(type(sample.capture_timestamp_ns), type(sample.mag_tesla))
        
        mag_size = sum(sys.getsizeof(v) for v in sample.mag_tesla)
        # print(f"Sending Size of accel0: {mag_size} bytes")

        mag_array = np.array(sample.mag_tesla, dtype=np.float32)

        if self.mode == "raw":            
            self.send_on_netmq("magneto", {"values": mag_array.tolist()})  
        elif self.mode == "processed":
            self.send_data("magneto", magneto_data)     

    def on_baro_received(self, sample):
        baro_data = {"timestamp": sample.capture_timestamp_ns, "pressure": sample.pressure}
        self.visualizer.sensor_plot["baro"].add_samples(sample.capture_timestamp_ns, [sample.pressure])
        #self.send_data("baro", baro_data)
        #print(f"Size of baro_data: {sys.getsizeof(baro_data)} bytes")  # Print size
        #print(type(sample.capture_timestamp_ns), type(sample.pressure))
        baro_array = np.array(sample.pressure, dtype=np.float32)

        if self.mode == "raw":
            self.send_on_netmq("baro", {"values": baro_array.tolist()})  
        elif self.mode == "processed":
            self.send_data("baro", baro_data)
    
    def on_audio_received(self, audio_and_record, *args):
        audio_data = np.array(audio_and_record.data, dtype=np.float32)
        if len(audio_data) == 0:
            return

          # Ensure audio_and_record.data exists
        if not hasattr(audio_and_record, "data") or audio_and_record.data is None:
            print("Received empty audio data")
            return           
        
        # Normalize audio data
        audio_data /= np.max(np.abs(audio_data)) if np.max(np.abs(audio_data)) > 0 else 1
        

         # Ensure the shape is correct
        if audio_data.ndim == 1:
            # Assuming it's a flat array and needs to be reshaped
            if audio_data.size % 7 == 0:
                # print("KiranM: Audio Reshape.. ")
                new_audio_data = audio_data.reshape(7, -1)
            else:
                raise ValueError(f"Unexpected audio data size: {audio_data.size}, cannot reshape to (7, N)")

        if new_audio_data.shape[0] != 7:
            raise ValueError(f"Expected 7-channel audio input, but got shape {audio_data.shape}")
       


        # Mix to stereo within this function
        left_mix = (new_audio_data[0] + new_audio_data[2] + new_audio_data[4] + 0.5 * new_audio_data[6]) / 3.5
        right_mix = (new_audio_data[1] + new_audio_data[3] + new_audio_data[5] + 0.5 * new_audio_data[6]) / 3.5
        stereo_audio = np.vstack((left_mix, right_mix)).T  # Interleave left and right

        # Convert to int16 format for WAV file
        stereo_audio_int16 = np.int16(stereo_audio * 32767)


         # Generate timestamp
        timestamp_ns = time.time() * 1e9

        # Add first sample to visualizer
        self.visualizer.sensor_plot["audio"].add_samples(timestamp_ns, [audio_data[0]])

         # Print all audio samples
        #print("Audio Samples Received:")
        #for i, sample in enumerate(audio_data):
        #    print(f"Sample {i}: {sample}")

        #print(f"Sending Audio Frames: ")
        if self.mode == "raw":            
            self.send_on_netmq("audio", {"values": audio_data.tolist()})  
        elif self.mode == "processed":
            self.send_data("audio", {"timestamp": timestamp_ns, "audio": audio_data.tolist()})

    def stop(self):
        print("Stopping stream...")
        self.visualizer.stop()