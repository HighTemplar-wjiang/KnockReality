using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MicListener : MonoBehaviour
{
    // Settings.
    private const int FREQUENCY = 32000;
    private const int MIC_RECORD_SEC = 10;
    private const int BUFFER_SIZE = FREQUENCY * MIC_RECORD_SEC;
    private const float COOLDOWN = 1f;
    private Vector3 POSITION_OFFSET = new Vector3(0, 0, 0.05f);
    private Vector3 ROTATION_OFFSET = new Vector3(90, 90, 180);

    // Remote server for signal processing.
    public string server;
    private float[] send_buffer = new float[BUFFER_SIZE];
    private int data_size = 0;

    // Hands. 
    public GameObject lefthandObject;
    public GameObject righthandObject;

    // Mic.
    private float cooldownSec = COOLDOWN;
    private AudioClip mic;
    private int lastPos, pos;
    private int segment_counter;

    // Action object. 
    private bool objectStatus;
    public GameObject actionObject;

    // Start is called before the first frame update
    void Start()
    {
        // Start mic. 
        mic = Microphone.Start(null, true, MIC_RECORD_SEC, FREQUENCY);

        // Create an audio source.
        AudioSource audio = GetComponent<AudioSource>();
        audio.clip = AudioClip.Create("test", MIC_RECORD_SEC * FREQUENCY, mic.channels, FREQUENCY, false);
        audio.loop = true;

        // Hide the object.
        this.objectStatus = false;
        this.actionObject.SetActive(this.objectStatus);
    }
     
    // Update is called once per frame
    void Update()
    {
        // Set cool-down.
        if (this.cooldownSec >= 0)
        {
            this.cooldownSec -= Time.deltaTime;
        }

        // AttachObject(this.actionObject, righthandObject.transform.position, righthandObject.transform.rotation);
        pos = Microphone.GetPosition(null);
        // Debug.Log(string.Format("[Mic] {0} {1}", pos, lastPos));
        if (pos > 0)
        {
            if(lastPos > pos)
            {
                lastPos = 0;
            }
            
            if(pos - lastPos > 0)
            {
                // Allocate buffer for mic data. 
                // Note: the number of samples taken from mic is decided by this buffer size. 
                var buffer = new float[(pos - lastPos) * mic.channels];

                // Get data from mic.
                mic.GetData(buffer, lastPos);
                System.Array.Copy(buffer, 0, this.send_buffer, this.data_size, buffer.Length);
                this.data_size += buffer.Length;

                // Set sampled data to audio source.
                AudioSource audio = GetComponent<AudioSource>();
                audio.clip.SetData(buffer, lastPos);

                // Post data when possible. 
                if (this.cooldownSec < 0)
                {
                    var post_data = new float[this.data_size];
                    System.Array.Copy(this.send_buffer, post_data, this.data_size);
                    StartCoroutine(PostAudioClip(post_data, this.data_size));
                    this.cooldownSec = COOLDOWN;
                    this.data_size = 0;
                }

                // Debug.
                // Debug.Log("[Audio]" + string.Join(" ", sample));
                /*
                float sampleSum = 0;
                foreach(float samplePoint in buffer)
                {
                    sampleSum += System.Math.Abs(samplePoint);
                }*/
                // Debug.Log("[Audio]" + sampleSum.ToString());
                

                if (!audio.isPlaying)
                {
                    // audio.Play();
                }

                lastPos = pos;
            }
        }
    }

    private void AttachObject(GameObject attachment, Vector3 position, Quaternion rotation)
    {
        // Modify rotation to either flat or vertical. 
        Quaternion newRotation;
        if(System.Math.Abs(rotation.eulerAngles.z) < 45)
        {
            // Vertical display.
            newRotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            attachment.transform.position = position;
            attachment.transform.rotation = newRotation;
            attachment.transform.Rotate(ROTATION_OFFSET);
            attachment.transform.Translate(POSITION_OFFSET);
        }
        else
        {
            // Flat display. 
            newRotation = Quaternion.Euler(90, rotation.eulerAngles.y + 90, 0);
            attachment.transform.position = position;
            attachment.transform.rotation = newRotation;
            attachment.transform.Rotate(ROTATION_OFFSET);
            attachment.transform.Rotate(0, 0, 90);
            attachment.transform.Translate(POSITION_OFFSET);
        }

        // Activate object.
        attachment.SetActive(true);
        Animator animator = attachment.GetComponent<Animator>();
        animator.SetBool("isShown", true);
    }

    // Streaming audio data.
    IEnumerator PostAudioClip(float[] buffer, int data_size)
    {
        // Convert audio sample to text.
        var jsonString = string.Format("{{\"instances\": [{0}] }}", string.Join(",", buffer));
        var jsonBinary = System.Text.Encoding.UTF8.GetBytes(jsonString);

        // Init handlers.
        DownloadHandlerBuffer downloadHandlerBuffer = new DownloadHandlerBuffer();
        UploadHandlerRaw uploadHandlerRaw = new UploadHandlerRaw(jsonBinary);
        uploadHandlerRaw.contentType = "application/json";

        // Send request.
        UnityWebRequest www =
            new UnityWebRequest(server, "POST", downloadHandlerBuffer, uploadHandlerRaw);
        yield return www.SendWebRequest();

        // Check results.
        if(www.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError(string.Format("{0}: {1}", www.url, www.error));
            www.Dispose();
        }
        else
        {
            KnockDetectorResult jsonResult = KnockDetectorResult.CreateFromJSON(www.downloadHandler.text);
            // Debug.Log("[Audio] results: " + www.downloadHandler.text);
            Debug.Log("[Audio] results: " + jsonResult.ToString());
            if (jsonResult.top_class_name != "_silence_")
            {
                AttachObject(this.actionObject, righthandObject.transform.position, righthandObject.transform.rotation);
            }

            www.Dispose();
        }
        yield break;
    }

    private void OnDestroy()
    {
        Microphone.End(null);
    }
}
