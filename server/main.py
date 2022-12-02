import json
import time
import uvicorn
import requests
import numpy as np
from fastapi import FastAPI, Request, BackgroundTasks
from audio_stream_receiver import AudioStreamBuffer

# For plotting.
import matplotlib.pyplot as plt
import matplotlib.animation as animation

# Settings.
SAMPLING_RATE = 16000
AUDIO_SEGMENT_SIZE = 34816
TENSORFLOW_SERVER_ENDPOINT = r"http://10.100.237.77:18501/v1/models/knock_detector:predict"

# Init server app.
app = FastAPI()

# Init audio receiver.
audio_buffer = AudioStreamBuffer(buffer_size_sec=10, stride_sec=0, sampling_rate=16000)


@app.get("/")
async def root():
    return {"message": "Hello World"}


@app.post("/audio/forward")
async def audio_forward(request: Request):
    post_body = await request.body()
    post_body = post_body.decode()
    result = requests.post(
        TENSORFLOW_SERVER_ENDPOINT,
        data=post_body
    )

    return result.json()


@app.post("/audio/stream")
async def audio_stream_receiver(request: Request, background_tasks: BackgroundTasks):
    # Get JSON body.
    stream_body = await request.body()
    # print(stream_body)
    stream_json = json.loads(stream_body.decode())

    # Push data stream.
    audio_buffer.append_audio_segment(stream_json["instances"])

    # Run recognition when possible.
    audio_segment = audio_buffer.take_segment(AUDIO_SEGMENT_SIZE)
    if audio_segment is not None:
        # Post to tensorflow server.
        audio_segment = audio_segment.tolist()
        json_body = json.dumps({
            "instances": [audio_segment]
        })
        # NOTE: using synchronized model for returning the result to Unity.
        result = requests.post(
            TENSORFLOW_SERVER_ENDPOINT,
            data=json_body
        )
        prediction_result = result.json()
        idx_top_class = np.argmax(prediction_result["predictions"])
        print(prediction_result, idx_top_class)

        # Plotting.
        fig, ax = plt.subplots()
        ax.plot(np.array(audio_segment).flatten())
        ax.set_title(str(prediction_result))
        fig.savefig("./log/audio_segment_{}.png".format(int(time.time())), dpi=150)

        # Constructing result.

        return_body = {
            "status": "ok",
            "predictions": prediction_result["predictions"],
            "top_class_name": "_silence_" if idx_top_class == 0 else "knock!"
        }
        return return_body
    else:
        return {
            "status": "ok",
            "predictions": [0.0, 0.0, 0.0],
            "top_class_name": "_silence_"
        }


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
