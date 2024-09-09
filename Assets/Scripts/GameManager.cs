using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public enum GameMode{
    Normal,
    Degree90,
    Degree180,
    Degree270,
    Random,
    Random2,
    HalfRandom,
    FullRandom, // 무리배치(겹노트) 해결해야함
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string SceneMusicSelect = "MusicSelect";
    public const string SceneInGame = "InGame";

    private Coroutine previewCoroutine;
    private readonly List<int> scoreEarly = new() { 0, 0, 0, 0, 0 };
    private readonly List<int> scoreLate = new() { 0, 0, 0, 0, 0 };

    public AudioSource GoSound;
    public AudioSource ReadySound;
    public AudioSource ResultSound;

    public float ClapVolume { get; set; } = 0f;
    public float StartTime { get; private set; } = -1;
    public AudioSource BackgroundSource { get; private set; }
    public Dictionary<string, float> MusicOffsetList { get; } = new();

    public GameMode CurrentMode { get; set; } = GameMode.Normal;
    public Music CurrentMusic { get; private set; }
    public Chart CurrentChart => CurrentMusic?.GetChart(CurrentDifficulty);
    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Extreme;
    public List<int> CurrentMusicBarScore { get; private set; } = new(new int[120]);

    private bool autoPlay;    
    public bool AutoPlay
    {
        get => autoPlay; 
        private set
        {
            if (StartTime > 0)
            {
                // 게임이 시작된 상태에선 자동 재생 불가능
                return;
            }
            autoPlay = value;
        }
    }

    private JudgementType currentJudgement = JudgementType.Normal;
    public JudgementType CurrentJudgement
    {
        get => currentJudgement;
        set
        {
            if (StartTime <= 0)
            {
                currentJudgement = value;
            }
        }
    }

    public int Combo { get; private set; }
    public int ShutterPoint { get; private set; }

    public int Score => 90_000 * (
        10 * (scoreEarly[0] + scoreLate[0]) + 
        7 * (scoreEarly[1] + scoreLate[1]) + 
        4 * (scoreEarly[2] + scoreLate[2]) +
        scoreEarly[3] + scoreLate[3]) / CurrentChart.NoteCount;

    public int ShutterScore => ShutterPoint * 100000 / 1024;

    private void Awake()
    {
        if (Instance == null && SceneManager.GetActiveScene().name != SceneMusicSelect)
        {
            Destroy(gameObject);
            SceneManager.LoadScene(SceneMusicSelect);
            return;
        }

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

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

    public void AddScore(JudgeState judgeState, int musicBarIndex, bool early = false)
    {
        var judge = (int) judgeState;
        if (early)
        {
            ++scoreEarly[judge];
        }
        else
        {
            ++scoreLate[judge];
        }
        GameObject.Find("Score").GetComponent<Text>().text = $"{Score}";

        if (musicBarIndex is >= 0 and < 120)
        {
            CurrentMusicBarScore[musicBarIndex] += judgeState switch
            {
                JudgeState.Perfect => 2,
                JudgeState.Poor or JudgeState.Miss => -10,
                _ => 1
            };
        }

        Combo = judge < 3 ? Combo + 1 : 0;
        GameObject.Find("Combo").GetComponent<Text>().text = Combo > 4 ? $"{Combo}" : "";

        switch (judge)
        {
            case < 2:
                ShutterPoint += Mathf.FloorToInt(2048f / Mathf.Min(1024, CurrentChart.NoteCount));
                break;
            case 2:
                ShutterPoint += Mathf.FloorToInt(1024f / Mathf.Min(1024, CurrentChart.NoteCount));
                break;
            default:
                ShutterPoint -= Mathf.FloorToInt(8192f / Mathf.Min(1024, CurrentChart.NoteCount));
                break;
        }
        ShutterPoint = Mathf.Max(Mathf.Min(1024, ShutterPoint), 0);
    }

    public void AddMusicOffset(float offset)
    {
        if (CurrentMusic == null)
        {
            return;
        }
        SetMusicOffset(MusicOffsetList[CurrentMusic.Title] + offset);
    }

    public void SetMusicOffset(float offset)
    {
        if (CurrentMusic == null)
        {
            return;
        }

        SetMusicOffset(CurrentMusic.Title, offset);
    }

    public void SetMusicOffset(string title, float offset)
    {
        MusicOffsetList[title] = offset;
    }

    public float GetMusicOffset()
    {
        return CurrentMusic == null ? 0 : GetMusicOffset(CurrentMusic.Title);
    }

    public float GetMusicOffset(string title)
    {
        MusicOffsetList.TryAdd(title, 0);
        return MusicOffsetList[title];
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case SceneMusicSelect:
                if (CurrentMusic != null)
                {
                    SelectMusic(CurrentMusic);
                }
                break;
            case SceneInGame:
                Combo = 0;
                ShutterPoint = 0;
                CurrentMusicBarScore = new(new int[120]);
                for (var i = 0; i < 4; ++i)
                {
                    scoreLate[i] = 0;
                    scoreEarly[i] = 0;
                }
                break;
        }
    }

    // HACK: GameObject.OnClick등록을 위해 정의
    public void SetJudgement(int judgement)
    {
        SetJudgement((JudgementType) judgement);
    }

    public void SetJudgement(JudgementType judgement)
    {
        CurrentJudgement = judgement;
    }

    // HACK: GameObject.OnClick등록을 위해 정의
    public void SetGameMode(int gameMode)
    {
        SetGameMode((GameMode) gameMode);
    }

    public void SetGameMode(GameMode gameMode)
    {
        CurrentMode = gameMode;
    }

    public void ToggleAutoMode()
    {
        AutoPlay = !AutoPlay;
        UIManager.Instance.GetUIObject<Button>("AutoMode").GetComponentInChildren<Text>().text = "자동: " + (AutoPlay ? "켜짐" : "꺼짐");
    }

    public void ToggleClapSound()
    {
        ClapVolume = ClapVolume > 0 ? 0 : 0.5f;
        UIManager.Instance.GetUIObject<Button>("ClapSound").GetComponentInChildren<Text>().text = "박수: " + (ClapVolume > 0 ? "켜짐" : "꺼짐");
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
        uiManager.GetUIObject<Text>("SelectedMusicTitle").text = music.Title;
        uiManager.GetUIObject<Text>("SelectedMusicArtist").text = music.Author;
        uiManager.GetUIObject<Image>("SelectedMusicJacket").sprite = music.Jacket;
        previewCoroutine = StartCoroutine(PlayMusicPreview());
        for (var index = 0; index < 3; ++index)
        {
            var difficulty = (Difficulty) index;
            uiManager.GetUIObject<Button>($"{difficulty}Button").interactable = CurrentMusic.CanPlay(difficulty);
        }
        uiManager.DrawMusicBar();
        if (CurrentChart != null)
        {
            uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + CurrentChart.Level;
            uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + CurrentChart.Score;
        }
        else
        {
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
        uiManager.DrawMusicBar();
        uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + CurrentChart.Level;
        uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + CurrentChart.Score;
    }

    public IEnumerator PlayMusicPreview()
    {
        if (CurrentMusic == null)
        {
            yield break;
        }

        var music = CurrentMusic;
        BackgroundSource.clip = music.Clip;
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
        SceneManager.LoadScene(SceneInGame);
        StartCoroutine(StartGame());
    }

    private void PlayClip()
    {
        BackgroundSource.clip = CurrentMusic.Clip;
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

        List<Note> noteList;
        switch (CurrentMode)
        {
            case GameMode.Degree90:
                noteList = CurrentChart.NoteList.Select(note => note.Rotate(90)).ToList();
                break;
            case GameMode.Degree180:
                noteList = CurrentChart.NoteList.Select(note => note.Rotate(180)).ToList();
                break;
            case GameMode.Degree270:
                noteList = CurrentChart.NoteList.Select(note => note.Rotate(270)).ToList();
                break;
            case GameMode.Random:
                List<int> row = new();
                List<int> column = new();
                while (row.Count < 4)
                {
                    var random = Random.Range(0, 4);
                    if (!row.Contains(random))
                    {
                        row.Add(random);
                    }
                }
                while (column.Count < 4)
                {
                    var random = Random.Range(0, 4);
                    if (!column.Contains(random))
                    {
                        column.Add(random);
                    }
                }
                noteList = CurrentChart.NoteList.Select(note => note.Random(row, column)).ToList();
                break;
            case GameMode.Random2:
                List<int> position = new();
                while (position.Count < 16)
                {
                    var random = Random.Range(0, 16);
                    if (!position.Contains(random))
                    {
                        position.Add(random);
                    }
                }
                noteList = CurrentChart.NoteList.Select(note => note.Random(position)).ToList();
                break;
            /*case GameMode.FullRandom:
                break;
            case GameMode.HalfRandom:
                break;*/
            default:
                noteList = CurrentChart.NoteList;
                break;
        }

        // TODO: Ready, GO 연출을 좀더 맛깔나게
        var comboText = UIManager.Instance.GetUIObject<Text>("Combo");
        comboText.fontSize = 160;
        ReadySound.Play();
        comboText.text = "Ready";
        yield return new WaitForSeconds(1.9f);

        GoSound.Play();
        comboText.text = "Go";
        yield return new WaitForSeconds(1.1f);
        comboText.text = "";
        comboText.fontSize = 300;

        if (CurrentMusic.StartOffset < 0)
        {
            PlayClip();
            yield return new WaitForSeconds(-CurrentMusic.StartOffset);
        }
        else
        {
            Invoke(nameof(PlayClip), CurrentMusic.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in noteList)
        {
            StartCoroutine(ShowMarker(note));
        }
        foreach (var time in CurrentChart.ClapTimings)
        {
            StartCoroutine(PlayClapForAuto((float)time));
        }

        if (CurrentMusic.StartOffset > 0)
        {
            yield return new WaitForSeconds(CurrentMusic.StartOffset);
        }

        var lastIndex = 0;
        var divide = BackgroundSource.clip.length / 120;
        while (BackgroundSource.isPlaying)
        {
            // BUG: 반영 상태가 느리거나 안됨
            var index = Mathf.FloorToInt((BackgroundSource.time - 23f / 30) / divide);
            if (index > 0 && index != lastIndex)
            {
                UIManager.Instance.UpdateMusicBar(lastIndex);
                lastIndex = index;
            }
            yield return null;
        }
        yield return new WaitForSeconds(.2f);
        ResultSound.Play();

        // TODO: result animation
        comboText.text = "";

        var scoreText = UIManager.Instance.GetUIObject<Text>("Score");
        var elapsedTime = 0f;
        while (elapsedTime < .6f)
        {
            elapsedTime += Time.deltaTime;
            scoreText.text = Score + Mathf.RoundToInt(ShutterScore * Mathf.Clamp01(elapsedTime / .6f)) + "";
            yield return null;
        }

        yield return new WaitForSeconds(.6f);

        comboText.fontSize = 200;
        comboText.text = "Cleared\n";

        //TODO: NEXT 버튼, Rating 추가
        scoreText.text = ShutterScore + Score + "";
        CurrentMusic.SetScore(CurrentDifficulty, ShutterScore + Score);
        CurrentMusic.SetMusicBarScore(CurrentDifficulty, CurrentMusicBarScore);

        for (int i = 0, limit = (int) JudgeState.Miss; i <= limit; ++i)
        {
            var judge = (JudgeState)i;
            UIManager.Instance.GetUIObject<Text>($"{judge}Text").text =
                $"{judge.ToString().ToUpper()}|\t{scoreEarly[i] + scoreLate[i]}";
        }
    }

    public void QuitGame()
    {
        if (StartTime <= 0)
        {
            return;
        }
        StartTime = -1;
        BackgroundSource.Stop();
        StopAllCoroutines();
        _ = ModifyMusicOffset(CurrentMusic.Title, CurrentMusic.StartOffset);
        SceneManager.LoadScene(SceneMusicSelect);
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
        //var text = GameObject.Find("Measure").GetComponent<Text>();
        yield return new WaitForSeconds((float)note.StartTime);
        MarkerManager.Instance.ShowMarker(note);
        //text.text = note.MeasureIndex + "";
    }

    private IEnumerator PlayClapForAuto(float delay)
    {
        yield return new WaitForSeconds(delay + 0.48333f - 0.140f); // 판정점 프레임 추가
        MarkerManager.Instance.PlayClap();
    }
}