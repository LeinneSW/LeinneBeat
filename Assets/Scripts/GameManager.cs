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

    private Coroutine previewCoroutine = null;
    private readonly List<int> scores = new() { 0, 0, 0, 0 };
    private readonly Dictionary<string, float> musicOffsetList = new();

    public Font textFont;
    public AudioSource goEffect;
    public AudioSource readyEffect;
    public AudioSource resultEffect;
    public readonly List<Music> musicList = new();

    public float ClapVolume { get; set; } = 0f;
    public float StartTime { get; private set; } = -1;
    public bool AutoMode { get; set; } = false;
    public AudioSource BackgroundSource { get; private set; }

    public Music SelectedMusic { get; private set; } = null;
    public Chart SelectedChart { get => SelectedMusic?.GetChart(SelectedDifficulty); }
    public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Extreme;

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

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (SceneManager.GetActiveScene().name != SCENE_MUSIC_SELECT)
        {
            SceneManager.LoadScene(SCENE_MUSIC_SELECT);
        }

        StartCoroutine(LoadGameData());

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
        if (SelectedMusic == null)
        {
            return;
        }
        SetMusicOffset(musicOffsetList[SelectedMusic.name] + offset);
    }

    public void SetMusicOffset(float offset)
    {
        if (SelectedChart == null)
        {
            return;
        }
        musicOffsetList[SelectedMusic.name] = offset;
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
                if (SelectedMusic != null)
                {
                    SelectMusic(SelectedMusic);
                }
                break;
            case SCENE_IN_GAME:
                Combo = 0;
                ShutterPoint = 0;
                for (int i = 0; i < 4; ++i)
                {
                    scores[i] = 0;
                }
                break;
        }
    }

    string RemoveLastExtension(string path)
    {
        return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
    }

    private AudioType GetAudioType(string extension)
    {
        return extension.ToLower() switch
        {
            ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".wav" => AudioType.WAV,
            _ => AudioType.UNKNOWN,
        };
    }

    private IEnumerator LoadGameData()
    {
        var basePath = Path.Combine(Application.dataPath, "..", "Songs");
        var path = Path.Combine(basePath, "sync.txt");
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

        foreach (var dirPath in Directory.GetDirectories(basePath))
        {
            var musicName = Path.GetFileName(dirPath);
            var songFiles = Directory.GetFiles(dirPath, "song.*");
            if (songFiles.Length < 1)
            {
                Debug.Log($"mp3파일이 존재하지 않습니다. 폴더명: {musicName}");
                continue;
            }

            var musicPath = songFiles[0];
            using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + musicPath, GetAudioType(Path.GetExtension(musicPath)));
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"폴더: {musicPath}, 오류: {www.error}");
                continue;
            }

            if (MusicManager.TryParse(DownloadHandlerAudioClip.GetContent(www), dirPath, out Music music))
            {
                musicList.Add(music);
            }
        }
        UIManager.Instance.InitSelectMusicScene();
    }

    public void SelectMusic(Music music)
    {
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
        }

        // TODO: play select sound
        SelectedMusic = music;
        var uiManager = UIManager.Instance;
        uiManager.GetUIObject<Text>("SelectedMusicTtitle").text = music.name;
        previewCoroutine = StartCoroutine(PlayMusicPreview());
        for (int index = 0; index < 3; ++index)
        {
            Difficulty difficulty = (Difficulty) index;
            uiManager.GetUIObject<Button>($"{difficulty}Button").interactable = SelectedMusic.CanPlay(difficulty);
        }
        if (SelectedChart != null)
        {
            uiManager.DrawMusicBar(SelectedChart.MusicBar);
            uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + SelectedChart.level;
            uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + SelectedChart.Score;
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
        if (StartTime > 0 || SelectedDifficulty == difficulty || !SelectedMusic.CanPlay(difficulty))
        {
            // 게임이 시작된 경우엔 난이도가 변경되어선 안됨
            return;
        }
        // TODO: play difficulty sound
        SelectedDifficulty = difficulty;
        var uiManager = UIManager.Instance;
        uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + SelectedChart.level;
        uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + SelectedChart.Score;
        uiManager.DrawMusicBar(SelectedChart.MusicBar);
    }

    public IEnumerator PlayMusicPreview()
    {
        if (SelectedMusic == null)
        {
            yield break;
        }

        var music = SelectedMusic;
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
        BackgroundSource.clip = SelectedMusic.clip;
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

        if (SelectedMusic.StartOffset < 0)
        {
            PlayBGM();
            yield return new WaitForSeconds(-SelectedMusic.StartOffset);
        }
        else
        {
            Invoke(nameof(PlayBGM), SelectedMusic.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in SelectedChart.NoteList)
        {
            StartCoroutine(ShowMarker(note));
        }
        foreach (var time in SelectedChart.clapTimings)
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
        SelectedMusic.SetScore(SelectedDifficulty, ShutterScore + Score);
        //TODO: NEXT 버튼, Rating 추가
    }

    private void FinishGame()
    {
        StartTime = -1;
        BackgroundSource.Stop();
        StopAllCoroutines();
        _ = ModifyMusicOffset(SelectedMusic.name, SelectedMusic.StartOffset);
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