using ARETT;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using WebSocketSharp;

public class NewBehaviourScript : MonoBehaviour
{
    // connect the DtatProvider-Prefab from ARETT in the Unity Editor
    public DataProvider DataProvider;
    private ConcurrentQueue<Action> _mainThreadWorkQueue = new ConcurrentQueue<Action>();
    [SerializeField]
    private TextMeshPro textMeshPro;


    string url = "http://192.168.53.119:3000";

    IEnumerator Upload(string gazeData)
    {
		Debug.Log("1");
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        formData.Add(new MultipartFormDataSection("gazeData", gazeData));

        UnityWebRequest www = UnityWebRequest.Post(url, formData);
        Debug.Log("2");
        yield return www.SendWebRequest();
        Debug.Log("3");
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log("Form upload complete! - " + www.downloadHandler.text);
            textMeshPro.text = www.downloadHandler.text;
        }
    }

	IEnumerator GiveDataAccess(string webId)
	{
		Debug.Log("1");
		List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
		formData.Add(new MultipartFormDataSection("grantAccessTo", webId));

		UnityWebRequest www = UnityWebRequest.Post(url + "/grant-access", formData);
		Debug.Log("2");
		yield return www.SendWebRequest();
		Debug.Log("3");
		if (www.result != UnityWebRequest.Result.Success)
		{
			Debug.Log(www.error);
		}
		else
		{
			Debug.Log("Form upload complete! - " + www.downloadHandler.text);
		}
	}

	WebSocket ws;


	public void ClickButton()
	{
		Debug.Log("Button click");
		StartCoroutine(GiveDataAccess("https://solid.interactions.ics.unisg.ch/kayPod/profile/card#me"));
	}

	public void SetText(string nameToSet)
	{
		textMeshPro.text = nameToSet;
		// textMeshPro.setText(nameToSet);
		textMeshPro.ForceMeshUpdate(true);
	}



	// Start is called before the first frame update
	void Start()
    {
        Debug.Log("HEY START");
		ws = new WebSocket("ws://192.168.53.119:3001");
		ws.Connect();

		ws.OnMessage += (sender, e) =>
		{
			Debug.Log("Message Received from " + ((WebSocket)sender).Url + ", Data : " + e.Data);
			// textMeshPro.text = "TEST--";
			SetText(e.Data);
			// textMeshPro.text = e.Data;
		};

		StartArettData();
       
    }

    // Update is called once per frame
    void Update()
    {
        // Check if there is something to process
        if (!_mainThreadWorkQueue.IsEmpty)
        {
            // Process all commands which are waiting to be processed
            // Note: This isn't 100% thread save as we could end in a loop when there is still new data coming in.
            //       However, data is added slowly enough so we shouldn't run into issues.
            while (_mainThreadWorkQueue.TryDequeue(out Action action))
            {
                // Invoke the waiting action
                action.Invoke();
            }
        }
    }

    /// <summary>
    /// Starts the Coroutine to get Eye tracking data on the HL2 from ARETT.
    /// </summary>
    public void StartArettData()
    {
        StartCoroutine(SubscribeToARETTData());
    }

    /// <summary>
    /// Subscribes to newDataEvent from ARETT.
    /// </summary>
    /// <returns></returns>
    private IEnumerator SubscribeToARETTData()
    {
        //*
        _mainThreadWorkQueue.Enqueue(() =>
        {
            DataProvider.NewDataEvent += HandleDataFromARETT;
        });
        //*/

        print("subscribed to ARETT events");
        Debug.Log("subscribed to ARETT events");
        yield return null;

    }

    /// <summary>
    /// Unsubscribes from NewDataEvent from ARETT.
    /// </summary>
    public void UnsubscribeFromARETTData()
    {
        _mainThreadWorkQueue.Enqueue(() =>
        {
            DataProvider.NewDataEvent -= HandleDataFromARETT;
        });

    }


    int i = 0;
    string gazeData = "";

    /// <summary>
    /// Handles gaze data from ARETT and allows you to do something with it
    /// </summary>
    /// <param name="gd"></param>
    /// <returns></returns>
    public void HandleDataFromARETT(GazeData gd)
    {
        // Debug.Log("HEY UPDATE");


        // Some exemplary values from ARETT.
        // for a full list of available data see:
        // https://github.com/AR-Eye-Tracking-Toolkit/ARETT/wiki/Log-Format#gaze-data
        string t = "received GazeData\n";
        t += "EyeDataRelativeTimestamp:" + gd.EyeDataRelativeTimestamp;
        t += "\nGazeDirection: " + gd.GazeDirection;
        t += "\nGazePointWebcam: " + gd.GazePointWebcam;
        t += "\nGazeHasValue: " + gd.GazeHasValue;
        t += "\nGazePoint: " + gd.GazePoint;
        t += "\nGazePointMonoDisplay: " + gd.GazePointMonoDisplay;
        // Debug.Log(t);

        // Play around with this number or maybe send every x seconds for better data quality
        // if (i <= 300)
        // {
        //    gazeData += gazeDataAsCSV(gd) + ";";
        //    i++;
        //}
        // else {
        //    StartCoroutine(Upload(gazeData));
       //     gazeData = "";
        //    i = 0;
        // }

		
		if (ws == null)
		{
			return;
		}
			
		ws.Send(gazeDataAsCSV(gd));
			
	}

	// FROM https://github.com/AR-Eye-Tracking-Toolkit/ARETT/blob/master/Scripts/DataLogger.cs
	private static readonly CultureInfo ci = new CultureInfo("en-US");
	/// <summary>
	/// Queue of strings which are supposed to be logged together with the next gaze data
	/// </summary>
	private ConcurrentQueue<string> infoToLog = new ConcurrentQueue<string>();

	public string gazeDataAsCSV(GazeData gazeData)
	{
		// Start the resulting data string
		StringBuilder logStringBuilder = new StringBuilder();
	logStringBuilder.Append(gazeData.EyeDataTimestamp.ToString(ci));
			logStringBuilder.Append(",");
			// Note: Highest accuracy for the EyeDataRelativeTimestamp is 100ns so we don't loose information by outputting a fixed number of decimal places
			logStringBuilder.Append(gazeData.EyeDataRelativeTimestamp.ToString("F4", ci));
			logStringBuilder.Append(",");
			logStringBuilder.Append(gazeData.FrameTimestamp.ToString(ci));
			logStringBuilder.Append(",");
			logStringBuilder.Append(gazeData.IsCalibrationValid.ToString(ci));
			logStringBuilder.Append(",");
			logStringBuilder.Append(gazeData.GazeHasValue.ToString(ci));
			logStringBuilder.Append(",");

			// If we have valid gaze data process it
			if (gazeData.GazeHasValue)
			{
				// Append the info about the gaze to our log
				logStringBuilder.Append(gazeData.GazeOrigin.x.ToString("F5", ci));
				logStringBuilder.Append(",");
				logStringBuilder.Append(gazeData.GazeOrigin.y.ToString("F5", ci));
				logStringBuilder.Append(",");
				logStringBuilder.Append(gazeData.GazeOrigin.z.ToString("F5", ci));
				logStringBuilder.Append(",");
				logStringBuilder.Append(gazeData.GazeDirection.x.ToString("F5", ci));
				logStringBuilder.Append(",");
				logStringBuilder.Append(gazeData.GazeDirection.y.ToString("F5", ci));
				logStringBuilder.Append(",");
				logStringBuilder.Append(gazeData.GazeDirection.z.ToString("F5", ci));
				logStringBuilder.Append(",");

				// Did we hit any GameObject?
				logStringBuilder.Append(gazeData.GazePointHit);
				logStringBuilder.Append(",");

				// If we did hit something on the gaze ray, write the hit info to the log, otherwise simply write NA
				if (gazeData.GazePointHit)
				{
					logStringBuilder.Append(gazeData.GazePoint.x.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePoint.y.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePoint.z.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointName);
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointOnHit.x.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointOnHit.y.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointOnHit.z.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitPosition.x.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitPosition.y.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitPosition.z.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitRotation.x.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitRotation.y.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitRotation.z.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitScale.x.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitScale.y.ToString("F5", ci));
					logStringBuilder.Append(",");
					logStringBuilder.Append(gazeData.GazePointHitScale.z.ToString("F5", ci));
					logStringBuilder.Append(",");

					if (gazeData.GazePointLeftDisplay.HasValue)
					{
						logStringBuilder.Append(gazeData.GazePointLeftDisplay.Value.x.ToString("F5", ci));
						logStringBuilder.Append(",");
						logStringBuilder.Append(gazeData.GazePointLeftDisplay.Value.y.ToString("F5", ci));
						logStringBuilder.Append(",");
						logStringBuilder.Append(gazeData.GazePointLeftDisplay.Value.z.ToString("F5", ci));
						logStringBuilder.Append(",");
						logStringBuilder.Append(gazeData.GazePointRightDisplay.Value.x.ToString("F5", ci));
						logStringBuilder.Append(",");
						logStringBuilder.Append(gazeData.GazePointRightDisplay.Value.y.ToString("F5", ci));
						logStringBuilder.Append(",");
						logStringBuilder.Append(gazeData.GazePointRightDisplay.Value.z.ToString("F5", ci));
						logStringBuilder.Append(",");
					}
					else
{
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,");
}

logStringBuilder.Append(gazeData.GazePointMonoDisplay.x.ToString("F5", ci));
logStringBuilder.Append(",");
logStringBuilder.Append(gazeData.GazePointMonoDisplay.y.ToString("F5", ci));
logStringBuilder.Append(",");
logStringBuilder.Append(gazeData.GazePointMonoDisplay.z.ToString("F5", ci));
logStringBuilder.Append(",");

logStringBuilder.Append(gazeData.GazePointWebcam.x.ToString("F5", ci));
logStringBuilder.Append(",");
logStringBuilder.Append(gazeData.GazePointWebcam.y.ToString("F5", ci));
logStringBuilder.Append(",");
logStringBuilder.Append(gazeData.GazePointWebcam.z.ToString("F5", ci));
logStringBuilder.Append(",");
				}
				else
{
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,");
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,");
}

// Did we hit an AOI?
logStringBuilder.Append(gazeData.GazePointAOIHit);
logStringBuilder.Append(",");

// If we hit an AOI, write the hit info to the log, otherwise simply write NA
if (gazeData.GazePointAOIHit)
{
	logStringBuilder.Append(gazeData.GazePointAOI.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOI.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOI.z.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIName);
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIOnHit.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIOnHit.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIOnHit.z.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitPosition.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitPosition.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitPosition.z.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitRotation.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitRotation.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitRotation.z.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitScale.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitScale.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIHitScale.z.ToString("F5", ci));
	logStringBuilder.Append(",");

	logStringBuilder.Append(gazeData.GazePointAOIWebcam.x.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIWebcam.y.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.GazePointAOIWebcam.z.ToString("F5", ci));
}
else
{
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,");
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA");
}
			}
			else
{
	// No gaze data
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,");
	// No gaze hit
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,");
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,");
	// No AOI hit
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,");
	logStringBuilder.Append("NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA,NA");
}

// If we are supposed to log the position of game objects, log them
// Note: We log the position even when we have no gaze data!
for (int i = 0; i < gazeData.positionInfos.Length; i++)
{
	// Make sure the object does have a valid position
	if (!gazeData.positionInfos[i].positionValid)
	{
		logStringBuilder.Append(",NA,NA,NA,NA,NA,NA,NA,NA,NA");
		continue;
	}

	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].xPosition.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].yPosition.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].zPosition.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].xRotation.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].yRotation.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].zRotation.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].xScale.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].yScale.ToString("F5", ci));
	logStringBuilder.Append(",");
	logStringBuilder.Append(gazeData.positionInfos[i].zScale.ToString("F5", ci));
}

// Append the separator for the info to the output string
logStringBuilder.Append(",");

// If there is info we should log, log it
if (!infoToLog.IsEmpty)
{
	while (infoToLog.TryDequeue(out string info))
	{
		// Append the string
		logStringBuilder.Append(info);

		// If there are more strings to append, add a separator between them
		if (infoToLog.Count > 0)
		{
			logStringBuilder.Append(";");
		}
	}
}
		logStringBuilder.Append(",NA,NA,NA,NA,NA,NA,NA,NA,NA");
		return logStringBuilder.ToString();



}

}