using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class KnockDetectorResult
{
    public string status;
    public float[] predictions;
    public string top_class_name;

    public static KnockDetectorResult CreateFromJSON(string jsonString)
    {
        return JsonUtility.FromJson<KnockDetectorResult>(jsonString);
    }

    public override string ToString()
    {
        return string.Format("{0} {1} {2}", this.status, this.predictions.ToString(), this.top_class_name);
    }
}
