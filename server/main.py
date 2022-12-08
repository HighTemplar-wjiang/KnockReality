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
SAMPLING_RATE = 32000
AUDIO_SEGMENT_SIZE = 34816
TENSORFLOW_SERVER_ENDPOINT = r"http://192.168.137.242:18501/v1/models/knock_detector:predict"
CLASSNAMES = ["_silence_", "knock_calendar", "knock_ironlocker", "knock_tabletop"]

# Init server app.
app = FastAPI()

# Init audio receiver.
audio_buffer = AudioStreamBuffer(buffer_size_sec=10, stride_sec=0.8, sampling_rate=SAMPLING_RATE)


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
    return_result = {
        "status": "ok",
        "predictions": [0.0, 0.0, 0.0, 0.0],
        "top_class_name": "_silence_"
    }

    # return return_result

    # Sliding the whole buffer.
    while True:
        audio_segment = audio_buffer.take_segment(AUDIO_SEGMENT_SIZE)
        # print("sliding...")
        if audio_segment is not None:

            # Sanity check.
            if np.max(audio_segment) < 0.01:
                # Silence.
                # audio_buffer.reset()
                continue

            # Normalization.
            audio_segment = audio_segment / np.max(np.abs(audio_segment)) * 0.15

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

            # Plotting.
            fig, ax = plt.subplots()
            ax.plot(np.array(audio_segment).flatten())
            ax.set_title(str(prediction_result))
            fig.savefig("./log/audio_segment_{}.png".format(int(time.time())), dpi=150)

            # Get class name.
            if np.max(prediction_result["predictions"]) < 0.6:
                return_class = "_silence_"
            else:
                return_class = CLASSNAMES[idx_top_class]
            print(prediction_result, return_class)

            # Constructing result.
            if ((return_class != "_silence_")
                    and (np.max(prediction_result["predictions"]) > np.max(return_result["predictions"]))):
                return_result = {
                    "status": "ok",
                    "predictions": prediction_result["predictions"],
                    "top_class_name": return_class
                }
                audio_buffer.reset()
        else:
            break

    # End of sliding.
    # audio_buffer.reset()
    return return_result


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
