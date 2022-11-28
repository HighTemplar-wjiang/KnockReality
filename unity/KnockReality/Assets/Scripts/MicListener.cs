using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicListener : MonoBehaviour
{

    // Settings.
    private const int FREQUENCY = 44100;
    private const float COOLDOWN = 1f;

    // Mic.
    private float cooldownSec = COOLDOWN;
    private AudioClip mic;
    private int lastPos, pos;

    // Action object. 
    private bool objectStatus;
    public GameObject actionObject;

    // Start is called before the first frame update
    void Start()
    {
        // Start mic. 
        mic = Microphone.Start(null, true, 10, FREQUENCY);

        // Create an audio source.
        AudioSource audio = GetComponent<AudioSource>();
        audio.clip = AudioClip.Create("test", 10 * FREQUENCY, mic.channels, FREQUENCY, false);
        audio.loop = true;

        // Hide the object.
        this.objectStatus = false;
        this.actionObject.SetActive(this.objectStatus);
    }

    // Update is called once per frame
    void Update()
    {
        if((pos = Microphone.GetPosition(null)) > 0)
        {
            if(lastPos > pos)
            {
                lastPos = 0;
            }
            
            if(pos - lastPos > 0)
            {
                // Allocate sample buffer. 
                float[] sample = new float[(pos - lastPos) * mic.channels];

                // Get data from mic.
                mic.GetData(sample, lastPos);

                // Set sampled data to audio source.
                AudioSource audio = GetComponent<AudioSource>();
                audio.clip.SetData(sample, lastPos);

                // Debug.
                // Debug.Log("[Audio]" + string.Join(" ", sample));
                float sampleSum = 0;
                foreach(float samplePoint in sample)
                {
                    sampleSum += System.Math.Abs(samplePoint);
                }
                // Debug.Log("[Audio]" + sampleSum.ToString());
                if(this.cooldownSec < 0)
                {
                    if (sampleSum > 0.3)
                    {
                        this.objectStatus = !this.objectStatus;
                        this.actionObject.SetActive(this.objectStatus);
                        this.cooldownSec = COOLDOWN;
                    }
                }
                else
                {
                    this.cooldownSec -= Time.deltaTime;
                }
                

                if (!audio.isPlaying)
                {
                    // audio.Play();
                }

                lastPos = pos;
            }
        }
    }

    private void OnDestroy()
    {
        Microphone.End(null);
    }
}
