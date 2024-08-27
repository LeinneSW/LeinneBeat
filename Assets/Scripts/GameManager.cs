using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; } = null;
    
    public const string SCENE_MUSIC_SELECT = "MusicSelect";
    public const string SCENE_IN_GAME = "InGame";

    private Chart currentChart = null;
    private AudioSource bgmSource;
    private readonly List<Chart> chartList = new();
    private readonly Dictionary<string, float> musicOffsetList = new();


    private readonly List<int> scores = new()
    {
        0, 0, 0, 0
    };

    public Font textFont;
    public AudioSource goEffect;
    public AudioSource readyEffect;
    public AudioSource resultEffect;
    public GameObject buttonPrefab;

    public float StartTime { get; private set; } = -1;
    public bool AutoMode { get; set; } = false;
    public float ClapVolume { get; set; } = 0f;

    public int Combo { get; private set; } = 0;
    public int ShutterPoint { get; private set; } = 0;
    public int Score
    {
        get => Mathf.FloorToInt(0.9f * Mathf.Floor(1000000 * (scores[0] + 0.7f * scores[1] + 0.4f * scores[2] + 0.1f * scores[3]) / currentChart.NoteCount));
    }
    public int ShutterScore
    {
        get => ShutterPoint * 100000 / 1024;
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(LoadGameData());

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = false;
        bgmSource.volume = 0;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(Screen.height * 10 / 16, Screen.height, true);
    }

    public void AddScore(int judge)
    {
        if(judge < 4)
        {
            ++scores[judge];
            GameObject.Find("Score").GetComponent<Text>().text = Score + "";
        }

        Combo = judge < 3 ? Combo + 1 : 0;
        GameObject.Find("Combo").GetComponent<Text>().text = Combo > 4 ? Combo + "" : "";

        if (judge < 2)
        {
            ShutterPoint += Mathf.FloorToInt(2048f / Mathf.Min(1024, currentChart.NoteCount));
        }
        else if (judge == 2)
        {
            ShutterPoint += Mathf.FloorToInt(1024f / Mathf.Min(1024, currentChart.NoteCount));
        }
        else
        {
            ShutterPoint -= Mathf.FloorToInt(8192f / Mathf.Min(1024, currentChart.NoteCount));
        }
        ShutterPoint = Mathf.Max(Mathf.Min(1024, ShutterPoint), 0);
    }

    public void AddMusicOffset(float offset)
    {
        if (currentChart == null)
        {
            return;
        }
        SetMusicOffset(musicOffsetList[currentChart.Name] + offset);
    }

    public void SetMusicOffset(float offset)
    {
        if (currentChart == null)
        {
            return;
        }
        musicOffsetList[currentChart.Name] = offset;
        var offsetText = GameObject.Find("MusicOffset");
        var text = offsetText.GetComponent<InputField>();
        text.text = "" + musicOffsetList[currentChart.Name];
    }

    public float GetMusicOffset(string name)
    {
        if (!musicOffsetList.ContainsKey(name))
        {
            musicOffsetList[name] = 0;
        }
        return musicOffsetList[name];
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case SCENE_MUSIC_SELECT:
                CreateSelectMusicUI();
                break;
            case SCENE_IN_GAME:
                if (currentChart == null)
                {
                    SceneManager.LoadScene(SCENE_MUSIC_SELECT);
                    return;
                }

                Combo = 0;
                ShutterPoint = 0;
                for (int i = 0; i < 4; ++i)
                {
                    scores[i] = 0;
                }
                var offsetText = GameObject.Find("MusicOffset");
                var text = offsetText.GetComponent<InputField>();
                text.text = "" + currentChart.StartOffset;
                text.onValueChanged.AddListener((value) =>
                {
                    if (float.TryParse(value, out var offset))
                    {
                        SetMusicOffset(offset);
                    }
                });
                for (int i = 1; i < 4; ++i)
                {
                    var number = 1 / Math.Pow(10, i);
                    var plusButton = GameObject.Find("+" + number);
                    var btn = plusButton.GetComponent<Button>();
                    btn.onClick.AddListener(() => AddMusicOffset((float)number));

                    var minusButton = GameObject.Find("-" + number);
                    btn = minusButton.GetComponent<Button>();
                    btn.onClick.AddListener(() => AddMusicOffset((float)-number));
                }
                var titleText = GameObject.Find("MusicTitle");
                var title = titleText.GetComponent<Text>();
                title.text = currentChart.Name;

                var autoButton = GameObject.Find("AutoButton").GetComponent<Button>();
                autoButton.onClick.AddListener(() => {
                    AutoMode = !AutoMode;
                    autoButton.transform.GetChild(0).GetComponent<Text>().text = "현재: " + (AutoMode ? "On" : "Off");
                });
                break;
        }
    }

    private void CreateSelectMusicUI()
    {
        var content = GameObject.Find("ChartScroll").transform.GetChild(0).GetChild(0).GetComponent<RectTransform>();
        //Vector2 position = new(0, 1000);
        for (int i = 0; i < chartList.Count; ++i)
        {
            var button = Instantiate(buttonPrefab, content);
            var chart = chartList[i];
            button.GetComponent<Button>().onClick.AddListener(() => PlayChart(chart));
            button.GetComponentInChildren<Text>().text = $"{chart.Name}({chart.Difficulty})";

            //CreateUIButton(content, $"{chart.Name}({chart.Difficulty})", position, i);
            //position -= new Vector2(0, 150);
        }
        float contentHeight = chartList.Count * (buttonPrefab.GetComponent<RectTransform>().rect.height + 15);
        content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);
    }

    void CreateUIButton(GameObject canvas, string buttonText, Vector2 position, int index)
    {
        // Button 게임오브젝트 생성
        GameObject buttonObject = new("Button");
        buttonObject.transform.SetParent(canvas.transform);

        // RectTransform 설정
        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(1000, 140); // 버튼 크기 설정
        rectTransform.anchoredPosition = position;

        // Button 컴포넌트 추가
        Button button = buttonObject.AddComponent<Button>();

        // Button 이미지 설정
        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.white; // 버튼 색상 설정

        // 텍스트 생성 및 설정
        GameObject textObject = new("Text");
        textObject.transform.SetParent(buttonObject.transform);

        Text text = textObject.AddComponent<Text>();
        text.text = buttonText;
        text.font = textFont;
        text.fontSize = 50;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;

        // 텍스트의 RectTransform 설정
        RectTransform textRectTransform = textObject.GetComponent<RectTransform>();
        textRectTransform.sizeDelta = rectTransform.sizeDelta;
        textRectTransform.anchoredPosition = Vector2.zero;

        // 버튼 클릭 이벤트 추가
        //button.onClick.AddListener(() => PlayChart(index));
    }

    string RemoveLastExtension(string filePath)
    {
        int lastDotIndex = filePath.LastIndexOf('.');
        // 마지막 '.' 이후에 확장자가 있는지 확인 (경로의 마지막 부분인지)
        if (lastDotIndex >= 0 && lastDotIndex > filePath.LastIndexOf(Path.DirectorySeparatorChar))
        {
            return filePath[..lastDotIndex];
        }
        return filePath;
    }

    IEnumerator LoadGameData()
    {
        var path = Path.Combine(Application.dataPath, "Songs", "sync.txt");
        if (File.Exists(path))
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                var split = line.Trim().Split(":");
                if(split.Length < 2)
                {
                    continue;
                }
                if (float.TryParse(split[1].Trim(), out float value))
                {
                    musicOffsetList[split[0].Trim()] = value;
                }
            }
        }

        string[] files = Directory.GetFiles(Path.Combine(Application.dataPath, "Songs"), "*.mp3");
        foreach (var musicPath in files)
        {
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, AudioType.MPEG);
            yield return www.SendWebRequest();

            var musicName = Path.GetFileNameWithoutExtension(musicPath);
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("알 수 없는 오류가 발생했습니다. musicPath: " + musicName);
                continue;
            }

            var removeExt = RemoveLastExtension(musicPath);
            var difficultyList = new List<string>()
            {
                "basic", "advanced", "extreme"
            };

            var charts = new List<Chart>();
            foreach (var difficulty in difficultyList)
            {
                var filePath = $"{removeExt}_{difficulty}.txt";
                if (!File.Exists(filePath))
                {
                    Debug.Log($"채보파일이 존재하지 않습니다. 파일명: {musicName}_{difficulty}");
                    continue;
                }

                var chart = Chart.Parse(musicName, difficulty, filePath);
                if (chart == null)
                {
                    Debug.Log($"채보파일이 잘못되었습니다. 파일명: {musicName}_{difficulty}");
                }
                else
                {
                    var _ = chart.StartOffset; // TODO: remove HACK
                    chart.bgmClip = DownloadHandlerAudioClip.GetContent(www);
                    chartList.Add(chart);
                }
            }
        }
        if (SceneManager.GetActiveScene().name == SCENE_MUSIC_SELECT)
        {
            CreateSelectMusicUI();
        }
    }

    public void PlayChart(Chart chart)
    {
        SceneManager.LoadScene(SCENE_IN_GAME);
        currentChart = chart;
        StartCoroutine(StartGame());
    }

    private void PlayBGM()
    {
        bgmSource.clip = currentChart.bgmClip;
        bgmSource.volume = 0.3f;
        bgmSource.Play();
    }

    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(.35f);
        var comboText = GameObject.Find("Combo").GetComponent<Text>(); // TODO: Hack this code
        comboText.fontSize = 150;
        readyEffect.Play();
        comboText.text = "Ready";
        yield return new WaitForSeconds(1.8f);

        goEffect.Play();
        comboText.text = "Go";
        yield return new WaitForSeconds(1f);
        comboText.text = "";
        comboText.fontSize = 300;

        if (currentChart.StartOffset < 0)
        {
            PlayBGM();
            yield return new WaitForSeconds(-currentChart.StartOffset);
        }
        else
        {
            Invoke(nameof(PlayBGM), currentChart.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in currentChart.AllNotes)
        {
            StartCoroutine(ShowMarker(note));
        }

        foreach (var time in currentChart.clapTimings)
        {
            StartCoroutine(PlayClapForAuto((float)time));
        }

        yield return new WaitForSeconds(5f);
        while (bgmSource.isPlaying)
        {
            yield return null;
        }
        resultEffect.Play();

        // TODO: result animation
        var scoreText = GameObject.Find("Score").GetComponent<Text>();
        float elapsedTime = 0f;
        while (elapsedTime < .8f)
        {
            elapsedTime += Time.deltaTime;
            scoreText.text = Score + Mathf.RoundToInt(ShutterScore * Mathf.Clamp01(elapsedTime / .8f)) + "";
            yield return null;
        }
        scoreText.text = ShutterScore + Score + "";
        //TODO: NEXT 버튼, Rating 추가
    }

    private void FinishGame()
    {
        StartTime = -1;
        bgmSource.Stop();
        StopAllCoroutines();
        _ = ModifyMusicOffset(currentChart.Name, currentChart.StartOffset);
        SceneManager.LoadScene(SCENE_MUSIC_SELECT);
    }

    private async Task ModifyMusicOffset(string name, float startOffset)
    {
        var path = Path.Combine(Application.dataPath, "Songs", "sync.txt");
        List<string> lines;
        if (File.Exists(path))
        {
            lines = new(await File.ReadAllLinesAsync(path));
        }
        else
        {
            lines = new();
        }

        bool find = false;
        for (int i = lines.Count - 1; i >= 0; --i)
        {
            var line = lines[i];
            if (lines[i].StartsWith($"{name}:", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{name}:{startOffset}";
                if (line != lines[i])
                {
                    find = true;
                    break;
                }
                return; // 동일할경우 저장하지 않음
            }
        }
        if (!find)
        {
            lines.Add($"{name}:{startOffset}");
        }
        await File.WriteAllLinesAsync(path, lines);
    }

    private IEnumerator ShowMarker(Note note)
    {
        var text = GameObject.Find("Measure").GetComponent<Text>();
        yield return new WaitForSeconds((float)note.StartTime);
        MarkerManager.Instance.ShowMarker(note);
        text.text = note.MeasureIndex + "";
    }

    private IEnumerator PlayClapForAuto(float delay)
    {
        yield return new WaitForSeconds(delay + 0.48333f - 0.140f); // 판정점 프레임 추가
        MarkerManager.Instance.PlayClap();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && StartTime != -1)
        {
            FinishGame();
            return;
        }
    }
}