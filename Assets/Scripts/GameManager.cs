using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameMode{
    Normal,
    Degree90,
    Degree180,
    Degree270,
    Random, // Not implemented: 무리배치 이슈
    FullRandom, // Not implemented: 무리배치 이슈
    HalfRandom,
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; } = null;

    public const string SCENE_MUSIC_SELECT = "MusicSelect";
    public const string SCENE_IN_GAME = "InGame";

    private Coroutine previewCoroutine = null;
    private readonly List<int> scores = new() { 0, 0, 0, 0 };
    public Dictionary<string, float> musicOffsetList = new();

    public Font textFont;
    public AudioSource goEffect;
    public AudioSource readyEffect;
    public AudioSource resultEffect;

    public float ClapVolume { get; set; } = 0f;
    public float StartTime { get; private set; } = -1;
    public bool AutoMode { get; private set; } = false;
    public AudioSource BackgroundSource { get; private set; }

    public GameMode CurrentMode { get; set; } = GameMode.Normal;
    public Music CurrentMusic { get; private set; } = null;
    public Chart CurrentChart { get => CurrentMusic?.GetChart(CurrentDifficulty); }
    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Extreme;

    public int Combo { get; private set; } = 0;
    public int ShutterPoint { get; private set; } = 0;

    public int Score
    {
        get => 90_000 * (10 * scores[0] + 7 * scores[1] + 4 * scores[2] + scores[3]) / CurrentChart.NoteCount;
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

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (SceneManager.GetActiveScene().name != SCENE_MUSIC_SELECT)
        {
            SceneManager.LoadScene(SCENE_MUSIC_SELECT);
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        // TODO: 곡 미선택시의 기본 배경음악 추가
        BackgroundSource = gameObject.AddComponent<AudioSource>();
        BackgroundSource.loop = false;
        BackgroundSource.volume = 0;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.SetResolution(Screen.height * 10 / 16, Screen.height, true);
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
            ShutterPoint += Mathf.FloorToInt(2048f / Mathf.Min(1024, CurrentChart.NoteCount));
        }
        else if (judge == 2)
        {
            ShutterPoint += Mathf.FloorToInt(1024f / Mathf.Min(1024, CurrentChart.NoteCount));
        }
        else
        {
            ShutterPoint -= Mathf.FloorToInt(8192f / Mathf.Min(1024, CurrentChart.NoteCount));
        }
        ShutterPoint = Mathf.Max(Mathf.Min(1024, ShutterPoint), 0);
    }

    public void AddMusicOffset(float offset)
    {
        if (CurrentMusic == null)
        {
            return;
        }
        SetMusicOffset(musicOffsetList[CurrentMusic.title] + offset);
    }

    public void SetMusicOffset(float offset)
    {
        if (CurrentChart == null)
        {
            return;
        }
        musicOffsetList[CurrentMusic.title] = offset;
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
                if (CurrentMusic != null)
                {
                    SelectMusic(CurrentMusic);
                }
                break;
            case SCENE_IN_GAME:
                Combo = 0;
                ShutterPoint = 0;
                for (int i = 0; i < 4; ++i)
                {
                    scores[i] = 0;
                }
                var autoButton = UIManager.Instance.GetUIObject<Button>("AutoButton");
                autoButton.onClick.AddListener(() => {
                    AutoMode = !AutoMode;
                    autoButton.GetComponentInChildren<Text>().text = "현재: " + (AutoMode ? "On" : "Off");
                });
                break;
        }
    }

    public void SelectMusic(Music music)
    {
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
        }

        // TODO: play select sound
        CurrentMusic = music;
        var uiManager = UIManager.Instance;
        uiManager.GetUIObject<Text>("SelectedMusicTtitle").text = music.title;
        uiManager.GetUIObject<Image>("SelectedMusicJacket").sprite = music.jacket;
        previewCoroutine = StartCoroutine(PlayMusicPreview());
        for (int index = 0; index < 3; ++index)
        {
            Difficulty difficulty = (Difficulty) index;
            uiManager.GetUIObject<Button>($"{difficulty}Button").interactable = CurrentMusic.CanPlay(difficulty);
        }
        if (CurrentChart != null)
        {
            uiManager.DrawMusicBar(CurrentChart.MusicBar);
            uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + CurrentChart.level;
            uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + CurrentChart.Score;
        }
        else
        {
            uiManager.DrawMusicBar(new());
            uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "채보 없음";
            uiManager.GetUIObject<Text>("SelectedMusicScore").text = "0";
        }
    }

    public void SelectDifficulty(Difficulty difficulty)
    {
        if (StartTime > 0 || CurrentDifficulty == difficulty || !CurrentMusic.CanPlay(difficulty))
        {
            return;
        }

        // TODO: play difficulty sound
        CurrentDifficulty = difficulty;
        var uiManager = UIManager.Instance;
        uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + CurrentChart.level;
        uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + CurrentChart.Score;
        uiManager.DrawMusicBar(CurrentChart.MusicBar);
    }

    public IEnumerator PlayMusicPreview()
    {
        if (CurrentMusic == null)
        {
            yield break;
        }

        var music = CurrentMusic;
        BackgroundSource.clip = music.clip;
        while (true)
        {
            BackgroundSource.volume = 0;
            BackgroundSource.Play();
            BackgroundSource.time = 30f;
            
            // 1.3초 페이드 인
            while (BackgroundSource.volume < .35f)
            {
                BackgroundSource.volume += .35f * Time.deltaTime / 1.3f;
                yield return null;
            }
            BackgroundSource.volume = .35f;

            // 12초 동안 재생
            yield return new WaitForSeconds(12);

            // 2초 페이드 아웃
            var startVolume = BackgroundSource.volume;
            while (BackgroundSource.volume > 0)
            {
                BackgroundSource.volume -= startVolume * Time.deltaTime / 2f;
                yield return null;
            }
            BackgroundSource.Stop();
            yield return new WaitForSeconds(1);
        }
    }

    public void PlayMusic()
    {
        if (CurrentChart == null)
        {
            return;
        }
        SceneManager.LoadScene(SCENE_IN_GAME);
        StartCoroutine(StartGame());
    }

    private void PlayBGM()
    {
        // TODO: 볼륨 조절 노브 추가
        BackgroundSource.clip = CurrentMusic.clip;
        BackgroundSource.volume = 0.35f;
        BackgroundSource.Play();
    }

    private IEnumerator StartGame()
    {
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
            yield return null;
        }

        UIManager.Instance.DrawMusicBar(CurrentChart.MusicBar);
        BackgroundSource.Stop();
        yield return new WaitForSeconds(.1f);
        // TODO: Ready, GO 연출을 좀더 맛깔나게
        var comboText = UIManager.Instance.GetUIObject<Text>("Combo");
        comboText.fontSize = 160;
        readyEffect.Play();
        comboText.text = "Ready";
        yield return new WaitForSeconds(1.9f);

        goEffect.Play();
        comboText.text = "Go";
        yield return new WaitForSeconds(1.1f);
        comboText.text = "";
        comboText.fontSize = 300;

        if (CurrentMusic.StartOffset < 0)
        {
            PlayBGM();
            yield return new WaitForSeconds(-CurrentMusic.StartOffset);
        }
        else
        {
            Invoke(nameof(PlayBGM), CurrentMusic.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in CurrentChart.NoteList)
        {
            StartCoroutine(ShowMarker(note));
        }
        foreach (var time in CurrentChart.clapTimings)
        {
            StartCoroutine(PlayClapForAuto((float)time));
        }

        yield return new WaitForSeconds(5f);
        while (BackgroundSource.isPlaying)
        {
            yield return null;
        }
        yield return new WaitForSeconds(.2f);
        resultEffect.Play();

        // TODO: result animation
        comboText.text = "";

        var scoreText = UIManager.Instance.GetUIObject<Text>("Score");
        float elapsedTime = 0f;
        while (elapsedTime < .8f)
        {
            elapsedTime += Time.deltaTime;
            scoreText.text = Score + Mathf.RoundToInt(ShutterScore * Mathf.Clamp01(elapsedTime / .8f)) + "";
            yield return null;
        }
        scoreText.text = ShutterScore + Score + "";
        CurrentMusic.SetScore(CurrentDifficulty, ShutterScore + Score);
        //TODO: NEXT 버튼, Rating 추가
    }

    private void FinishGame()
    {
        StartTime = -1;
        BackgroundSource.Stop();
        StopAllCoroutines();
        _ = ModifyMusicOffset(CurrentMusic.title, CurrentMusic.StartOffset);
        SceneManager.LoadScene(SCENE_MUSIC_SELECT);
    }

    private async Task ModifyMusicOffset(string name, float startOffset)
    {
        var path = Path.Combine(Application.dataPath, "..", "Songs", "sync.txt");
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
        yield return new WaitForSeconds(delay + 0.48333f - 0.130f); // 판정점 프레임 추가
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