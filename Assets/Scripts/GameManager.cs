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
    private readonly List<Chart> chartList = new();
    private SemaphoreSlim chartSemaphore = new SemaphoreSlim(1, 1); // 최대 하나의 작업만 접근 허용

    private readonly List<int> scores = new() { 0, 0, 0, 0 };
    private readonly Dictionary<string, float> musicOffsetList = new();

    public Font textFont;
    public AudioSource goEffect;
    public AudioSource readyEffect;
    public AudioSource resultEffect;
    public GameObject buttonPrefab;

    public float ClapVolume { get; set; } = 0f;
    public float StartTime { get; private set; } = -1;
    public bool AutoMode { get; private set; } = false;
    public AudioSource BackgroundMusic { get; private set; }

    public int Combo { get; private set; } = 0;
    public int ShutterPoint { get; private set; } = 0;

    public int Score
    {
        get => 90_000 * (10 * scores[0] + 7 * scores[1] + 4 * scores[2] + scores[3]) / currentChart.NoteCount;
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

        BackgroundMusic = gameObject.AddComponent<AudioSource>();
        BackgroundMusic.loop = false;
        BackgroundMusic.volume = 0;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(Screen.height * 10 / 16, Screen.height, true);
    }

    public void AddScore(int judge)
    {
        if(judge < 4)
        {
            ++scores[judge];
            GameObject.Find("Score").GetComponent<Text>().text = $"{Score}";
        }

        Combo = judge < 3 ? Combo + 1 : 0;
        GameObject.Find("Combo").GetComponent<Text>().text = Combo > 4 ? $"{Combo}" : "";

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
                    autoButton.GetComponentInChildren<Text>().text = "현재: " + (AutoMode ? "On" : "Off");
                });
                break;
        }
    }

    private void CreateSelectMusicUI()
    {
        var content = GameObject.Find("ChartScroll").transform.GetComponentInChildren<RectTransform>();
        for (int i = 0; i < chartList.Count; ++i)
        {
            AddChartButton(content, chartList[i]);
        }
        float contentHeight = chartList.Count * (buttonPrefab.GetComponent<RectTransform>().rect.height + 15);
        content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);
    }

    string RemoveLastExtension(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
    }

    public async Task AddChart(Chart chart)
    {
        await chartSemaphore.WaitAsync(); // 비동기적으로 락을 요청
        try
        {
            chartList.Add(chart);
            if (SceneManager.GetActiveScene().name == SCENE_MUSIC_SELECT)
            {
                AddChartButton(chart);
            }
        }
        finally
        {
            chartSemaphore.Release(); // 락 해제
        }
    }

    private void AddChartButton(RectTransform content, Chart chart)
    {
        var button = Instantiate(buttonPrefab, content);
        button.GetComponent<Button>().onClick.AddListener(() => PlayChart(chart));
        button.GetComponentInChildren<Text>().text = $"{chart.Name}({chart.Difficulty})";

        /*float contentHeight = chartList.Count * (buttonPrefab.GetComponent<RectTransform>().rect.height + 15);
        content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);*/
    }

    private IEnumerator LoadGameData()
    {
        var path = Path.Combine(Application.dataPath, "Songs", "sync.txt");
        if (File.Exists(path))
        {
            Task<string[]> loadLinesTask = Task.Run(() => File.ReadAllLines(path));
            yield return new WaitUntil(() => loadLinesTask.IsCompleted);
            foreach (string line in loadLinesTask.Result)
            {
                var split = line.Trim().Split(":");
                if (split.Length > 1 && float.TryParse(split[1].Trim(), out float value))
                {
                    musicOffsetList[split[0].Trim()] = value;
                }
            }
        }

        Task<string[]> filesTaskList = Task.Run(() => Directory.GetFiles(Path.Combine(Application.dataPath, "Songs"), "*.mp3"));
        yield return new WaitUntil(() => filesTaskList.IsCompleted);

        List<Task> loadTasks = new();
        foreach (var musicPath in filesTaskList.Result)
        {
            loadTasks.Add(Task.Run(async () =>
            {
                var musicName = Path.GetFileNameWithoutExtension(musicPath);
                using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, AudioType.MPEG);
                await www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"음악 파일을 불러오지 못했습니다. 이름: {musicName}, 경로: {musicPath}");
                    return;
                }

                var removeExt = RemoveLastExtension(musicPath);
                var difficultyList = new List<string> { "basic", "advanced", "extreme" };

                foreach (var difficulty in difficultyList)
                {
                    var filePath = $"{removeExt}_{difficulty}.txt";
                    if (!File.Exists(filePath))
                    {
                        //Debug.Log($"채보파일이 존재하지 않습니다. 파일명: {musicName}_{difficulty}");
                        continue;
                    }

                    var chart = Chart.Parse(musicName, difficulty, filePath);
                    if (chart == null)
                    {
                        Debug.Log($"잘못된 채보입니다. 파일명: {musicName}_{difficulty}");
                    }
                    else
                    {
                        var _ = chart.StartOffset; // TODO: remove HACK
                        chart.bgmClip = DownloadHandlerAudioClip.GetContent(www);
                        AddChart(chart);
                    }
                }
            }));
        }

        // TODO: 대기해야하는지 확인해봐야함
        /*foreach (var task in loadTasks)
        {
            yield return new WaitUntil(() => task.IsCompleted);
        }*/
    }

    public void PlayChart(Chart chart)
    {
        SceneManager.LoadScene(SCENE_IN_GAME);
        currentChart = chart;
        StartCoroutine(StartGame());
    }

    private void PlayBGM()
    {
        // TODO: 볼륨 조절 노브 추가
        BackgroundMusic.clip = currentChart.bgmClip;
        BackgroundMusic.volume = 0.35f;
        BackgroundMusic.Play();
    }

    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(.35f);
        var comboText = GameObject.Find("Combo").GetComponent<Text>(); // TODO: Hack this code
        comboText.fontSize = 160;
        readyEffect.Play();
        comboText.text = "Ready";
        yield return new WaitForSeconds(1.8f);

        goEffect.Play();
        comboText.text = "Go";
        yield return new WaitForSeconds(1.1f);
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
        while (BackgroundMusic.isPlaying)
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
        BackgroundMusic.Stop();
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