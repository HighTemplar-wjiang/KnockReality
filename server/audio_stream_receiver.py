# AudioStreamReceiver.
import logging
import numpy as np
from circular_buffer import Queue


class AudioStreamBuffer:

    def __init__(self, buffer_size_sec=10, stride_sec=0.5, sampling_rate=16000):

        # Init params.
        self._sampling_rate = sampling_rate
        self._stride_sec = stride_sec
        self._stride_size = int(sampling_rate * stride_sec)
        self._buffer_size = buffer_size_sec * sampling_rate

        # Init buffer.
        self._idx_start = 0
        self._idx_end = 0  # Not inclusive.
        self._audio_buffer = Queue(shape=(self._buffer_size, 1))

    def reset(self):
        self._audio_buffer.reset()

    def append_audio_segment(self, audio_segment):
        if self._audio_buffer.isfull:
            # Buffer is full.
            logging.warning("Audio buffer is full, dropping incoming stream.")
            return False
        else:
            self._audio_buffer.enqueue(np.array(audio_segment))
            return True

    def take_segment(self, num_samples):
        # Take first num_samples and stride.
        if self._audio_buffer.length >= num_samples:
            output_segment = self._audio_buffer.peek_first_N(num_samples)
            if self._stride_size == 0:
                self._audio_buffer.dequeue(num_samples)
            else:
                self._audio_buffer.dequeue(self._stride_size)
            return output_segment
        else:
            logging.warning("Audio buffer does not have enough data.")
            return None



