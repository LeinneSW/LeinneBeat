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

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string SceneMusicSelect = "MusicSelect";
    public const string SceneInGame = "InGame";

    private Coroutine previewCoroutine;
    private readonly List<int> earlyJudgeList = new() { 0, 0, 0, 0, 0 };
    private readonly List<int> rateJudgeList = new() { 0, 0, 0, 0, 0 };

    public AudioSource GoSound;
    public AudioSource ReadySound;
    public AudioSource ResultSound;

    public bool IsStarted => StartTime > 0;
    public float StartTime { get; private set; } = -1;
    public AudioSource BackgroundSource { get; private set; }

    public Music CurrentMusic { get; private set; }
    public Chart CurrentChart => CurrentMusic?.GetChart(CurrentDifficulty);
    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Extreme;
    public List<int> CurrentMusicBarScore { get; } = new(new int[120]);

    public int Combo { get; private set; }
    public int ShutterPoint { get; private set; }
    public int Score => 90_000 * (
        10 * (earlyJudgeList[0] + rateJudgeList[0]) + 
        7 * (earlyJudgeList[1] + rateJudgeList[1]) + 
        4 * (earlyJudgeList[2] + rateJudgeList[2]) +
        earlyJudgeList[3] + rateJudgeList[3]) / CurrentChart.NoteCount;

    public int ShutterScore => ShutterPoint * 100000 / 1024;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != SceneInGame) return;
        // 인게임 화면에서 실행되는 경우엔 무조건 막히도록 설정
        if (Instance == null)
        {
            SceneManager.LoadScene(SceneMusicSelect);
        }
        Destroy(gameObject);
        return;
    }

    private void Start()
    {
        BackgroundSource = gameObject.AddComponent<AudioSource>();
        BackgroundSource.volume = GameOptions.Instance.MusicVolume;
        BackgroundSource.loop = false;

        if (Instance != null)
        {
            SetDifficulty(Instance.CurrentDifficulty);
            SelectMusic(Instance.CurrentMusic);
            Destroy(Instance.gameObject);
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddScore(JudgeState judgeState, int musicBarIndex, bool early = false)
    {
        var judge = (int) judgeState;
        if (early)
        {
            ++earlyJudgeList[judge];
        }
        else
        {
            ++rateJudgeList[judge];
        }
        GameObject.Find("Score").GetComponent<Text>().text = $"{Score}";

        if (musicBarIndex is >= 0 and < 120)
        {
            CurrentMusicBarScore[musicBarIndex] += judgeState switch
            {
                JudgeState.Perfect => 2,
                JudgeState.Poor or JudgeState.Miss => -100,
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

    // HACK: GameObject.OnClick등록을 위해 정의
    public void SetJudgement(int judgement)
    {
        SetJudgement((JudgementType) judgement);
    }

    public void SetJudgement(JudgementType judgement)
    {
        GameOptions.Instance.JudgementType = judgement;
    }

    // HACK: GameObject.OnClick등록을 위해 정의
    public void SetGameMode(int gameMode)
    {
        SetGameMode((GameMode) gameMode);
    }

    public void SetGameMode(GameMode gameMode)
    {
        GameOptions.Instance.GameMode = gameMode;
    }

    public void SelectMusic(Music music)
    {
        if (CurrentMusic == music)
        {
            return;
        }

        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
        }

        // TODO: play select sound
        CurrentMusic = music;
        var uiManager = UIManager.Instance;
        uiManager.GetUIObject<Text>("SelectedMusicTitle").text = music.Title;
        uiManager.GetUIObject<Text>("SelectedMusicArtist").text = music.Artist;
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

    public void SetDifficulty(Difficulty difficulty)
    {
        if (IsStarted || CurrentDifficulty == difficulty)
        {
            return;
        }

        // TODO: play difficulty sound
        CurrentDifficulty = difficulty;
        var uiManager = UIManager.Instance;
        uiManager.UpdateDifficulty();
        if (CurrentMusic == null || !CurrentMusic.CanPlay(difficulty)) return;
        uiManager.GetUIObject<Text>("SelectedMusicLevel").text = "" + CurrentChart.Level;
        uiManager.GetUIObject<Text>("SelectedMusicScore").text = "" + CurrentChart.Score;
    }

    public IEnumerator PlayMusicPreview()
    {
        if (CurrentMusic == null)
        {
            yield break;
        }

        BackgroundSource.Stop();
        yield return null;

        BackgroundSource.clip = CurrentMusic.Clip;
        while (true)
        {
            var maxVolume = GameOptions.Instance.MusicVolume;
            BackgroundSource.volume = 0;
            BackgroundSource.Play();
            BackgroundSource.time = 30f;

            while (BackgroundSource.volume < maxVolume) // 페이드 인
            {
                BackgroundSource.volume += maxVolume * Time.deltaTime / 1.2f; 
                yield return null;
            }
            BackgroundSource.volume = maxVolume;

            yield return new WaitForSeconds(12); // 12초 동안 재생

            var startVolume = maxVolume;
            while (BackgroundSource.volume > 0) // 페이드 아웃
            {
                BackgroundSource.volume -= startVolume * Time.deltaTime / 1.6f;
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

    private void StartMusicClip()
    {
        BackgroundSource.clip = CurrentMusic.Clip;
        BackgroundSource.volume = GameOptions.Instance.MusicVolume;
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
        switch (GameOptions.Instance.GameMode)
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
            case GameMode.HalfRandom:
            case GameMode.FullRandom:
                noteList = new ChartRandomHelper(CurrentChart.NoteList).Shuffle(GameOptions.Instance.GameMode);
                break;
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
            StartMusicClip();
            yield return new WaitForSeconds(-CurrentMusic.StartOffset);
        }
        else
        {
            Invoke(nameof(StartMusicClip), CurrentMusic.StartOffset);
        }

        StartTime = Time.time;
        foreach (var note in noteList)
        {
            StartCoroutine(ShowMarker(note));
        }

        if (GameOptions.Instance.AutoClap)
        {
            foreach (var time in CurrentChart.ClapTimings)
            {
                StartCoroutine(PlayClapForAuto((float)time));
            }
        }

        if (CurrentMusic.StartOffset > 0)
        {
            yield return new WaitForSeconds(CurrentMusic.StartOffset);
        }
        yield return null;

        var lastIndex = 0;
        var divide = BackgroundSource.clip.length / 120;
        while (BackgroundSource.isPlaying)
        {
            var index = Mathf.FloorToInt(BackgroundSource.time / divide);
            if (index > 0 && index != lastIndex)
            {
                UIManager.Instance.UpdateMusicBar(lastIndex, 23 / 30f);
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
        var totalScore = ShutterScore + Score;
        scoreText.text = totalScore + "";

        yield return new WaitForSeconds(.6f);

        var rating = totalScore switch
        {
            > 999999 => "EXC",
            > 979999 => "SSS",
            > 949999 => "SS",
            > 899999 => "S",
            > 849999 => "A",
            > 799999 => "B",
            > 699999 => "C",
            > 499999 => "D",
            _ => "E"
        };

        comboText.fontSize = 200;
        comboText.text = $"Cleared\n{rating}";

        for (int i = 0, limit = (int) JudgeState.Miss; i <= limit; ++i)
        {
            var judge = (JudgeState)i;
            UIManager.Instance.GetUIObject<Text>($"{judge}Text").text =
                $"{judge.ToString().ToUpper()}|\t{earlyJudgeList[i] + rateJudgeList[i]}";
        }

        if (GameOptions.Instance.AutoPlay) yield break;
        CurrentMusic.SetScore(CurrentDifficulty, totalScore);
        CurrentMusic.SetMusicBarScore(CurrentDifficulty, CurrentMusicBarScore);
        _ = MusicManager.Instance.SaveMusicScore(CurrentDifficulty, CurrentMusic);
    }

    public void QuitGame()
    {
        if (!IsStarted)
        {
            return;
        }
        BackgroundSource.Stop();
        StopAllCoroutines();
        _ = MusicManager.Instance.SaveMusicOffset(CurrentMusic.Title);
        SceneManager.LoadScene(SceneMusicSelect);
    }

    private IEnumerator ShowMarker(Note note)
    {
        yield return new WaitForSeconds((float)note.StartTime);
        MarkerManager.Instance.ShowMarker(note);
    }

    private IEnumerator PlayClapForAuto(float delay)
    {
        yield return new WaitForSeconds(delay + 0.48333f - 0.140f); // 판정점 프레임 추가
        MarkerManager.Instance.PlayClap();
    }
}