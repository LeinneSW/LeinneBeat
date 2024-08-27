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

    private readonly List<Chart> chartList = new();

    private Coroutine previewCoroutine = null;
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
    public Chart SelectedChart { get; private set; } = null;

    public int Combo { get; private set; } = 0;
    public int ShutterPoint { get; private set; } = 0;

    public int Score
    {
        get => 90_000 * (10 * scores[0] + 7 * scores[1] + 4 * scores[2] + scores[3]) / SelectedChart.NoteCount;
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
        StartCoroutine(LoadGameData());

        BackgroundMusic = gameObject.AddComponent<AudioSource>();
        BackgroundMusic.loop = false;
        BackgroundMusic.volume = 0;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(Screen.height * 10 / 16, Screen.height, true);

        if (SceneManager.GetActiveScene().name == SCENE_IN_GAME)
        {
            SceneManager.LoadScene(SCENE_MUSIC_SELECT);
        }
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void AddScore(int judge)
    {
        if (judge < 4)
        {
            ++scores[judge];
            GameObject.Find("Score").GetComponent<Text>().text = $"{Score}";
        }

        Combo = judge < 3 ? Combo + 1 : 0;
        GameObject.Find("Combo").GetComponent<Text>().text = Combo > 4 ? $"{Combo}" : "";

        if (judge < 2)
        {
            ShutterPoint += Mathf.FloorToInt(2048f / Mathf.Min(1024, SelectedChart.NoteCount));
        }
        else if (judge == 2)
        {
            ShutterPoint += Mathf.FloorToInt(1024f / Mathf.Min(1024, SelectedChart.NoteCount));
        }
        else
        {
            ShutterPoint -= Mathf.FloorToInt(8192f / Mathf.Min(1024, SelectedChart.NoteCount));
        }
        ShutterPoint = Mathf.Max(Mathf.Min(1024, ShutterPoint), 0);
    }

    public void AddMusicOffset(float offset)
    {
        if (SelectedChart == null)
        {
            return;
        }
        SetMusicOffset(musicOffsetList[SelectedChart.Name] + offset);
    }

    public void SetMusicOffset(float offset)
    {
        if (SelectedChart == null)
        {
            return;
        }
        musicOffsetList[SelectedChart.Name] = offset;
        var offsetText = GameObject.Find("MusicOffset");
        var text = offsetText.GetComponent<InputField>();
        text.text = "" + musicOffsetList[SelectedChart.Name];
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
                Combo = 0;
                ShutterPoint = 0;
                for (int i = 0; i < 4; ++i)
                {
                    scores[i] = 0;
                }
                var offsetText = GameObject.Find("MusicOffset");
                var text = offsetText.GetComponent<InputField>();
                text.text = "" + SelectedChart.StartOffset;
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
                title.text = SelectedChart.Name;

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
        GameObject.Find("StartGameButton").GetComponent<Button>().onClick.AddListener(() => PlayChart());
        var content = GameObject.Find("ChartScroll").transform.GetChild(0).GetChild(0).GetComponent<RectTransform>();
        for (int i = 0; i < chartList.Count; ++i)
        {
            AddChartButton(content, chartList[i]);
        }
        // 곡을 선택하여 무조건 재생되게함
        SelectChart(SelectedChart ?? chartList[0]);
    }

    string RemoveLastExtension(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
    }

    private void AddChartButton(RectTransform content, Chart chart)
    {
        var button = Instantiate(buttonPrefab, content);
        button.GetComponent<Button>().onClick.AddListener(() => SelectChart(chart));
        button.GetComponentInChildren<Text>().text = $"{chart.Name}({chart.Difficulty})";
    }

    private IEnumerator LoadGameData()
    {
        var path = Path.Combine(Application.dataPath, "Songs", "sync.txt");
        if (File.Exists(path))
        {
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                var split = line.Trim().Split(":");
                if (split.Length < 2)
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
                    //Debug.Log($"채보파일이 존재하지 않습니다. 파일명: {musicName}_{difficulty}");
                    continue;
                }

                var chart = Chart.Parse(musicName, difficulty, filePath);
                if (chart == null)
                {
                    Debug.Log($"채보파일이 잘못되었습니다. 파일명: {musicName}_{difficulty}");
                }
                else
                {
                    _ = chart.StartOffset; // TODO: remove HACK
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

    public void SelectChart(Chart chart)
    {
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
        }

        SelectedChart = chart;
        previewCoroutine = StartCoroutine(PlayMusicPreview());
    }

    public IEnumerator PlayMusicPreview()
    {
        var chart = SelectedChart;
        BackgroundMusic.clip = chart.bgmClip;
        while (SelectedChart == chart)
        {
            BackgroundMusic.time = 30f;
            BackgroundMusic.volume = 0;
            BackgroundMusic.Play();
            
            // 1.5초 페이드 인
            while (BackgroundMusic.volume < .35f)
            {
                BackgroundMusic.volume += .35f * Time.deltaTime / 1.5f;
                yield return null;
            }
            BackgroundMusic.volume = .35f;

            // 10초 동안 재생
            yield return new WaitForSeconds(15);

            // 1.5초 페이드 아웃
            var startVolume = BackgroundMusic.volume;
            while (BackgroundMusic.volume > 0)
            {
                BackgroundMusic.volume -= startVolume * Time.deltaTime / 1.5f;
                yield return null;
            }
            BackgroundMusic.Stop();
            yield return new WaitForSeconds(1);
        }
    }

    public void PlayChart()
    {
        if (SelectedChart == null)
        {
            return;
        }
        SceneManager.LoadScene(SCENE_IN_GAME);
        StartCoroutine(StartGame());
    }

    private void PlayBGM()
    {
        // TODO: 볼륨 조절 노브 추가
        BackgroundMusic.clip = SelectedChart.bgmClip;
        BackgroundMusic.volume = 0.35f;
        BackgroundMusic.Play();
    }

    private IEnumerator StartGame()
    {
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
            yield return null;
        }

        BackgroundMusic.Stop();
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

        if (SelectedChart.StartOffset < 0)
        {
            PlayBGM();
            yield return new WaitForSeconds(-SelectedChart.StartOffset);
        }
        else
        {
            Invoke(nameof(PlayBGM), SelectedChart.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in SelectedChart.AllNotes)
        {
            StartCoroutine(ShowMarker(note));
        }

        foreach (var time in SelectedChart.clapTimings)
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
        _ = ModifyMusicOffset(SelectedChart.Name, SelectedChart.StartOffset);
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