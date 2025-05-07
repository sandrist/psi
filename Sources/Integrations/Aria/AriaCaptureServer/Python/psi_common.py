import zmq
from datetime import datetime
import msgpack
import threading

_topic_message_times = dict()
_output_lock = threading.Lock()

def setup_input_socket(port):
    '''Setup an incoming socket'''

    inputSocket = zmq.Context().socket(zmq.SUB)
    inputSocket.connect(port)
    inputSocket.setsockopt(zmq.SUBSCRIBE, '')
    return inputSocket

def setup_output_socket(port):
    '''Setup an outgoing socket'''

    output_socket = zmq.Context().socket(zmq.PUB)
    output_socket.bind(port)
    return output_socket

def datetime_to_ticks(dt):
    '''Convert datetime to ticks, necessary for passing originating times via MessagePack'''

    t0 = datetime(1, 1, 1)
    seconds = (dt - t0).total_seconds()
    ticks = seconds * 10**7
    return int(ticks)

def send_topic_message(outputSocket, topicName, outputMessage, timestamp = None, encodeBinary=False):
    '''Send a given message on a given socket with specified topic name'''

    global _topic_message_times
    global _output_lock
    if timestamp == None:
        timestamp = datetime_to_ticks(datetime.utcnow())
    if topicName in _topic_message_times and timestamp <= _topic_message_times[topicName]:
        timestamp = _topic_message_times[topicName] + 1
    _topic_message_times[topicName] = timestamp
    payload = {}
    payload['message'] = outputMessage
    payload['originatingTime'] = timestamp
    with _output_lock:
        if encodeBinary:
            outputSocket.send_multipart([topicName.encode(), msgpack.packb(payload, use_bin_type=True)])
        else:
            outputSocket.send_multipart([topicName.encode(), msgpack.dumps(payload)])