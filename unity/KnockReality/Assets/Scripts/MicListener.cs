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
    private const float NETWORK_INTERVAL = 0.5f;
    private const float INTERACTION_COOLDOWN = 2.0f;
    private Vector3 POSITION_OFFSET = new Vector3(0, 0, 0.05f);
    private Vector3 ROTATION_OFFSET = new Vector3(90, 90, 180);

    // Remote server for signal processing.
    public string server;
    private float[] send_buffer = new float[BUFFER_SIZE];
    private int data_size = 0;

    // Hands and camera.
    public GameObject cameraRig;
    public GameObject lefthandObject;
    public GameObject righthandObject;

    // Mic.
    private float networkInterval = NETWORK_INTERVAL;
    private float interactionCooldown = INTERACTION_COOLDOWN;
    private AudioClip mic;
    private int lastPos, pos;
    private int segment_counter;

    // Action object. 
    private bool objectStatus;
    public GameObject actionObjectIronlocker;
    public GameObject actionObjectTabletop;
    public GameObject actionObjectCalendar;
    public GameObject officeModel;

    // Anchor.
    public GameObject anchorDesktop;
    private Vector3 originalPosition;

    // Passthrough.
    OVRPassthroughLayer passthroughLayer;

    // Debug.
    float switchTimer = 5.0f;

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
        this.actionObjectIronlocker.SetActive(this.objectStatus);

        // Get passthrough overlay.
        GameObject ovrCameraRig = GameObject.Find("OVRCameraRig");
        if (ovrCameraRig == null)
        {
            Debug.LogError("Scene does not contain an OVRCameraRig");
            return;
        }

        passthroughLayer = ovrCameraRig.GetComponent<OVRPassthroughLayer>();
        if (passthroughLayer == null)
        {
            Debug.LogError("OVRCameraRig does not contain an OVRPassthroughLayer component");
        }

        // Get original position for the desktop.
        originalPosition = anchorDesktop.transform.position;
        Debug.Log("[Desk] Orignal position: " + originalPosition.ToString());
    }
     
    // Update is called once per frame
    void Update()
    {
        // Debug.Log(string.Format("[Hand] {0}", this.righthandObject.transform.position.ToString()));
        // Set cool-down.
        if (this.networkInterval >= 0)
        {
            this.networkInterval -= Time.deltaTime;
        }

        if (this.interactionCooldown >= 0)
        {
            interactionCooldown -= Time.deltaTime;
        }

        this.switchTimer -= Time.deltaTime;
        if (this.switchTimer < 0)
        {
            // SwitchReality();
            this.switchTimer = 5.0f;
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
                if ((this.networkInterval < 0) && (this.interactionCooldown < 0))
                {
                    var post_data = new float[this.data_size];
                    System.Array.Copy(this.send_buffer, post_data, this.data_size);
                    StartCoroutine(PostAudioClip(post_data, this.data_size));
                    this.networkInterval = NETWORK_INTERVAL;
                    this.data_size = 0;
                }

                lastPos = pos;
            }
        }
    }

    private void SwitchReality()
    {
        if(passthroughLayer.hidden)
        {
            // Switch from VR to MR.
            passthroughLayer.hidden = false;
            officeModel.SetActive(false);
            officeModel.transform.position = originalPosition;
        }
        else
        {
            // Switch from MR to VR.
            passthroughLayer.hidden = true;
            officeModel.SetActive(true);

            anchorDesktop.transform.position = this.righthandObject.transform.position;
            anchorDesktop.transform.Translate(new Vector3(0.0f, 0.2f, 0.0f));
            anchorDesktop.transform.forward = cameraRig.transform.forward;
        }
    }

    private void AttachObject(GameObject attachment, Vector3 position, Quaternion rotation)
    {
        // Sanity check.
        if (attachment == null)
        {
            return;
        }

        // Modify rotation to either flat or vertical. 
        Quaternion newRotation;
        // if(System.Math.Abs(rotation.eulerAngles.z) < 45)
        if(true)
        {
            // Vertical display.
            // newRotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            attachment.transform.position = position;
            // attachment.transform.rotation = newRotation;
            // attachment.transform.Rotate(ROTATION_OFFSET);
            // attachment.transform.Translate(POSITION_OFFSET);
            attachment.transform.forward = cameraRig.transform.forward;
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

        if (attachment.activeSelf == false)
        {
            attachment.SetActive(true);
            Animator animator = attachment.GetComponent<Animator>();
            animator.SetBool("isShown", true);
        }
        else
        {
            attachment.SetActive(false);
            Animator animator = attachment.GetComponent<Animator>();
            animator.SetBool("isShown", false);
        }
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
            www.Dispose();

            // Debug.Log("[Audio] results: " + www.downloadHandler.text);
            Debug.Log("[Audio] results: " + jsonResult.ToString());
            this.interactionCooldown = INTERACTION_COOLDOWN;
            switch (jsonResult.top_class_name)
            {
                case "_silence_": 
                    this.interactionCooldown = -0.1f;
                    break;
                case "knock_tabletop":
                    SwitchReality();
                    break;
                case "knock_ironlocker":
                    AttachObject(this.actionObjectIronlocker, righthandObject.transform.position, righthandObject.transform.rotation);
                    break;
                case "knock_calendar":
                    AttachObject(this.actionObjectCalendar, righthandObject.transform.position, righthandObject.transform.rotation);
                    break;
                default:
                    break;
            }
        }
        yield break;
    }

    private void OnDestroy()
    {
        Microphone.End(null);
    }
}
