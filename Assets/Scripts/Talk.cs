using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using MiniJSON;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Connection;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.DataTypes;

public class Talk : MonoBehaviour
{

    #region Watson SpeechToTextの設定
    [Space(10)]
    [Header("Watson SpeechToText Config")]
    [Tooltip("SpeechToText service URL")]
    [SerializeField]
    private string sttServiceUrl = "";
    [Tooltip("The authentication api key.")]
    [SerializeField]
    private string sttApiKey = "";
    [Tooltip("The authentication token.")]
    [SerializeField]
    private string sttToken = "";
    #endregion

    #region TalkAPIの設定
    [Space(10)]
    [Header("TalkAPI Config")]
    [Tooltip("A3RT API URL")]
    [SerializeField]
    private string a3rtURL = "";
    [Tooltip("A3RT API Key")]
    [SerializeField]
    private string a3rtApiKey = "";
    #endregion

    #region Watson TextToSpeechの設定
    [Space(10)]
    [Header("Watson TextToSpeech Config")]
    [Tooltip("TextToSpeech service URL")]
    [SerializeField]
    private string ttsServiceUrl = "";
    [Tooltip("The authentication username.")]
    [SerializeField]
    private string ttsUsername = "";
    [Tooltip("The authentication password.")]
    [SerializeField]
    private string ttsPassword = "";
    #endregion

    #region STT用変数
    // SpeechToText
    private SpeechToText sttService;

    private int recordingRoutine = 0;
    private string microphoneID = null;
    private AudioClip recording = null;
    private int recordingBufferSize = 1;
    private int recordingHZ = 22050;

    private string recognizeText = null;
    #endregion

    #region TTS用変数
    // TextToSpeech
    private TextToSpeech ttsService;
    private bool synthesizeTested = false;
    #endregion

    private Animator anim;

    private string animatorTalkState = "close";

    void Start()
    {
        anim = GetComponent<Animator>();
        microphoneID = Microphone.devices[0];

        // TextToSpeech初期化
        Credentials ttsCredentials = new Credentials(ttsUsername, ttsPassword, ttsServiceUrl);
        ttsService = new TextToSpeech(ttsCredentials)
        {
            Voice = VoiceType.ja_JP_Emi
        };

        // SpeechToText初期化
        TokenOptions tokenOptions = new TokenOptions()
        {
            IamApiKey = sttApiKey,
            IamAccessToken = sttToken
        };
        Credentials sttCredentials = new Credentials(tokenOptions, sttServiceUrl);
        sttService = new SpeechToText(sttCredentials)
        {
            StreamMultipart = true
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("s"))
        {
            Active = true;
            StartRecording();
        }
        if (recognizeText != null)
        {
            var text = recognizeText;
            recognizeText = null;
            StartCoroutine(Chat(text));
        }
    }

    void OnGUI()
    {
        GUI.Box(new Rect(Screen.width - 110, 10, 100, 60), "雑談");
        if (GUI.Button(new Rect(Screen.width - 100, 40, 80, 20), "音声"))
        {
            Active = true;
            StartRecording();
        }

    }

    #region SpeechToText
    public bool Active
    {
        get { return sttService.IsListening; }
        set
        {
            if (value && !sttService.IsListening)
            {
                sttService.RecognizeModel = "ja-JP_BroadbandModel";
                sttService.DetectSilence = true;
                sttService.EnableWordConfidence = true;
                sttService.EnableTimestamps = true;
                sttService.SilenceThreshold = 0.01f;
                sttService.MaxAlternatives = 0;
                sttService.EnableInterimResults = true;
                sttService.OnError = SttOnError;
                sttService.InactivityTimeout = -1;
                sttService.ProfanityFilter = false;
                sttService.SmartFormatting = true;
                sttService.SpeakerLabels = false;
                sttService.WordAlternativesThreshold = null;
                sttService.StartListening(OnRecognize);
            }
            else if (!value && sttService.IsListening)
            {
                sttService.StopListening();
            }
        }
    }

    private void StartRecording()
    {
        if (recordingRoutine == 0)
        {
            UnityObjectUtil.StartDestroyQueue();
            recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    private void StopRecording()
    {
        if (recordingRoutine != 0)
        {
            Microphone.End(microphoneID);
            Runnable.Stop(recordingRoutine);
            recordingRoutine = 0;
        }
    }

    private void SttOnError(string error)
    {
        Active = false;
        Debug.LogFormat("SpeechToText Recording Error. {0}", error);
    }

    private IEnumerator RecordingHandler()
    {
        Debug.LogFormat("Start recording. devices: {0}", microphoneID);
        recording = Microphone.Start(microphoneID, true, recordingBufferSize, recordingHZ);
        yield return null;

        if (recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = recording.samples / 2;
        float[] samples = null;

        while (recordingRoutine != 0 && recording != null)
        {
            int writePos = Microphone.GetPosition(microphoneID);
            if (writePos > recording.samples || !Microphone.IsRecording(microphoneID))
            {
                Debug.LogErrorFormat("Recording Error. Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
              || (!bFirstBlock && writePos < midPoint))
            {
                samples = new float[midPoint];
                recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(Mathf.Abs(Mathf.Min(samples)), Mathf.Max(samples));
                record.Clip = AudioClip.Create("Recording", midPoint, recording.channels, recordingHZ, false);
                record.Clip.SetData(samples, 0);

                sttService.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                int remaining = bFirstBlock ? (midPoint - writePos) : (recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }

        }

        yield break;
    }

    private void OnRecognize(SpeechRecognitionEvent result, Dictionary<string, object> customData)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    var reply = alt.transcript;
                    if (res.final)
                    {
                        Debug.Log("[音声]" + reply);
                        Active = false;
                        StopRecording();
                        recognizeText = reply;
                        return;
                    }
                }
            }
        }
    }

    #endregion

    #region 雑談
    internal IEnumerator Chat(string text)
    {
        Debug.Log("Start Chat");
        WWWForm form = new WWWForm();
        form.AddField("apikey", a3rtApiKey);
        form.AddField("query", text);
        var url = a3rtURL;
        var request = UnityWebRequest.Post(url, form);
        yield return request.SendWebRequest();

        if (request.isHttpError || request.isNetworkError)
        {
            Debug.LogFormat("chat request failed. {0}", request.error);
        }
        else
        {
            if (request.responseCode == 200)
            {
                var json = Json.Deserialize(request.downloadHandler.text) as Dictionary<string, object>;
                if (json.ContainsKey("results"))
                {
                    var r = json["results"] as List<object>;
                    if (r.Count > 0)
                    {
                        var c = r[0] as Dictionary<string, object>;
                        var res = (string)c["reply"];
                        Debug.Log("[AI]" + res);
                        yield return Speech(res);
                    }
                }
            }
            else
            {
                Debug.LogFormat("chat response failed. response code:{0}", request.responseCode);
            }
        }
    }
    #endregion

    #region TextToSpeech
    private IEnumerator Speech(string message)
    {
        Debug.Log("Start Speech To Text");
        ttsService.ToSpeech(HandleToSpeechCallback, TtsOnError, message, true);
        while (!synthesizeTested)
            yield return null;

    }

    void HandleToSpeechCallback(AudioClip clip, Dictionary<string, object> customData = null)
    {
        if (Application.isPlaying && clip != null)
        {
            Debug.Log("Play speech");
            GameObject audioObject = new GameObject("AudioObject");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.0f;
            source.loop = false;
            source.clip = clip;
            source.Play();
            anim.Play(animatorTalkState, 1, 0.0f);
            Destroy(audioObject, clip.length);

            synthesizeTested = true;
        }
    }

    private void TtsOnError(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Debug.LogErrorFormat("TextToSpeech Error. {0}", error.ToString());
    }

    #endregion
}
