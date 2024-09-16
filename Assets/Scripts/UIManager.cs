using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private readonly Dictionary<string, Component> componentCache = new();

    public GameObject MusicButtonPrefab;

    private float beforeTime = 0;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public T GetUIObject<T>(string name) where T : Component
    {
        if (componentCache.TryGetValue(name, out var cacheComponent) && cacheComponent != null)
        {
            return cacheComponent as T;
        }

        var obj = GameObject.Find(name);
        if (obj == null)
        {
            Debug.LogWarning($"[UI 에러] '{name}' 라는 이름의 게임 오브젝트를 찾을 수 없었습니다.");
            return null;
        }

        if (!obj.TryGetComponent<T>(out var component))
        {
            component = obj.GetComponentInChildren<T>();
        }

        if (component == null)
        {
            Debug.LogWarning($"[UI 에러] '{name}' 라는 이름의 게임 오브젝트를 찾을 수 없었습니다.");
            return null;
        }
        componentCache[name] = component;
        return component;
    }

    public void AddMusicButton(Music music)
    {
        var content = GetUIObject<RectTransform>("MusicListContent");
        if (content == null) return;
        var button = Instantiate(MusicButtonPrefab, content);
        button.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.SelectMusic(music));
        button.transform.GetChild(0).GetComponent<Text>().text = $"{music.Title}{(music.IsLong ? " (홀드)" : "")}";
        button.transform.GetChild(1).GetComponent<Text>().text = music.Author;
        if (music.Jacket != null)
        {
            button.transform.GetChild(2).GetComponent<Image>().sprite = music.Jacket;
        }
    }

    public void SortMusicByName()
    {
        MusicManager.Instance.MusicList.Sort((x, y) => string.Compare(x.Title, y.Title, StringComparison.OrdinalIgnoreCase));
        ResetMusicList();
        foreach (var music in MusicManager.Instance.MusicList)
        {
            AddMusicButton(music);
        }
    }

    public void SortMusicByArtist()
    {
        MusicManager.Instance.MusicList.Sort((x, y) => string.Compare(x.Author, y.Author, StringComparison.OrdinalIgnoreCase));
        ResetMusicList();
        foreach (var music in MusicManager.Instance.MusicList)
        {
            AddMusicButton(music);
        }
    }

    public void SortMusicByScore()
    {
        var difficulty = GameManager.Instance.CurrentDifficulty;
        MusicManager.Instance.MusicList.Sort((x, y) => x.GetScore(difficulty).CompareTo(y.GetScore(difficulty)));
        ResetMusicList();
        foreach (var music in MusicManager.Instance.MusicList)
        {
            AddMusicButton(music);
        }
    }

    public void ResetMusicList()
    {
        var content = GetUIObject<RectTransform>("MusicListContent");
        if (content == null) return;
        foreach (Transform child in content)
        {
            Destroy(child.gameObject); // 기존 버튼 제거
        }
    }

    private void InitSelectMusicScene()
    {
        var sortByName = GetUIObject<Button>("SortByName");
        sortByName.onClick.AddListener(SortMusicByName);
        var sortByArtist = GetUIObject<Button>("SortByArtist");
        sortByArtist.onClick.AddListener(SortMusicByArtist);
        var sortByScore = GetUIObject<Button>("SortByScore");
        sortByScore.onClick.AddListener(SortMusicByScore);

        var settingButton = GetUIObject<Button>("SettingButton");
        settingButton.onClick.AddListener(ToggleSetting);

        GetUIObject<Button>("StartGameButton").onClick.AddListener(() => GameManager.Instance.PlayMusic());

        var basic = GetUIObject<Button>("BasicButton");
        basic.interactable = false;
        basic.onClick.AddListener(() => GameManager.Instance.SetDifficulty(Difficulty.Basic));
        var advanced = GetUIObject<Button>("AdvancedButton");
        advanced.interactable = false;
        advanced.onClick.AddListener(() => GameManager.Instance.SetDifficulty(Difficulty.Advanced));
        var extreme = GetUIObject<Button>("ExtremeButton");
        extreme.interactable = false;
        extreme.onClick.AddListener(() => GameManager.Instance.SetDifficulty(Difficulty.Extreme));
    }

    private void InitOptionUi()
    {
        var judgeDropdown = GetUIObject<Dropdown>("JudgeSettings");
        judgeDropdown.value = (int)GameOptions.Instance.JudgementType;
        judgeDropdown.onValueChanged.AddListener(value => GameOptions.Instance.JudgementType = (JudgementType)value);

        var randomDropdown = GetUIObject<Dropdown>("RandomSettings");
        randomDropdown.value = (int)GameOptions.Instance.GameMode;
        randomDropdown.onValueChanged.AddListener(value => GameOptions.Instance.GameMode = (GameMode)value);

        var autoButton = GetUIObject<Button>("AutoPlay");
        autoButton.GetComponentInChildren<Text>().text = GameOptions.Instance.AutoPlay ? "켜짐" : "꺼짐";
        autoButton.onClick.AddListener(() => GameOptions.Instance.AutoPlay = !GameOptions.Instance.AutoPlay);
        var clapButton = GetUIObject<Button>("AutoClap");
        clapButton.GetComponentInChildren<Text>().text = GameOptions.Instance.AutoClap ? "켜짐" : "꺼짐";
        clapButton.onClick.AddListener(() => GameOptions.Instance.AutoClap = !GameOptions.Instance.AutoClap);
    }

    public void ToggleSetting()
    {
        var pane = GetUIObject<Image>("SettingPane");
        var scale = pane.transform.localScale;
        pane.transform.localScale = scale.x > 0 ? new(0, 0) : new(1, 1);
    }

    public void UpdateDifficulty()
    {
        DrawMusicBar();
        var difficulty = GameManager.Instance.CurrentDifficulty;
        Dictionary<Difficulty, Color> color = new()
        {
            { Difficulty.Basic, new Color(0x92 / 255f, 0xF8 / 255f, 0x5C / 255f) },
            { Difficulty.Advanced, new Color(0xFF / 255f, 0xDF / 255f, 0x5D / 255f) },
            { Difficulty.Extreme, new Color(0xE7 / 255f, 0x5F / 255f, 0x65 / 255f) },
        };
        var basic = GetUIObject<Button>("BasicButton");
        var advanced = GetUIObject<Button>("AdvancedButton");
        var extreme = GetUIObject<Button>("ExtremeButton");
        switch (difficulty)
        {
            case Difficulty.Basic:
                basic.GetComponent<Image>().color = color[difficulty];
                advanced.GetComponent<Image>().color = Color.white;
                extreme.GetComponent<Image>().color = Color.white;
                break;
            case Difficulty.Advanced:
                basic.GetComponent<Image>().color = Color.white;
                advanced.GetComponent<Image>().color = color[difficulty];
                extreme.GetComponent<Image>().color = Color.white;
                break;
            default:
                basic.GetComponent<Image>().color = Color.white;
                advanced.GetComponent<Image>().color = Color.white;
                extreme.GetComponent<Image>().color = color[difficulty];
                break;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case GameManager.SceneMusicSelect:
                InitSelectMusicScene();
                InitOptionUi();
                break;
            case GameManager.SceneInGame:
                DrawMusicBar(false);

                var currentMusic = GameManager.Instance.CurrentMusic;
                GetUIObject<Text>("MusicTitle").text = currentMusic.Title;
                GetUIObject<Text>("MusicArtist").text = currentMusic.Author;
                var jacket = GetUIObject<Image>("MusicJacket");
                jacket.sprite = currentMusic.Jacket;
                jacket.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (Time.time - beforeTime <= 1)
                    {
                        GameManager.Instance.QuitGame();
                        return;
                    }
                    beforeTime = Time.time;
                });

                var offsetText = GetUIObject<InputField>("MusicOffset");
                offsetText.text = "" + GameManager.Instance.CurrentMusic.StartOffset;
                offsetText.onValueChanged.AddListener((value) =>
                {
                    if (float.TryParse(value, out var offset))
                    {
                        GameManager.Instance.CurrentMusic.StartOffset = offset;
                    }
                });
                for (var i = 1; i < 4; ++i)
                {
                    var number = 1 / Math.Pow(10, i);
                    var plusButton = GetUIObject<Button>("+" + number);
                    plusButton.onClick.AddListener(() =>
                    {
                        GameManager.Instance.CurrentMusic.StartOffset += (float) number;
                        offsetText.text = "" + GameManager.Instance.CurrentMusic.StartOffset;
                    });

                    var minusButton = GetUIObject<Button>("-" + number);
                    minusButton.onClick.AddListener(() =>
                    {
                        GameManager.Instance.CurrentMusic.StartOffset -= (float) number;
                        offsetText.text = "" + GameManager.Instance.CurrentMusic.StartOffset;
                    });
                }
                break;
        }
    }

    public void UpdateMusicBar(int index, float delay)
    {
        StartCoroutine(UpdateMusicBarCoroutine(index, delay));
    }

    private IEnumerator UpdateMusicBarCoroutine(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        var gridPanel = GetUIObject<RectTransform>("MusicBar");
        var musicBar = GameManager.Instance.CurrentChart.MusicBar;
        var color = GetMusicBarColor(musicBar[index], GameManager.Instance.CurrentMusicBarScore[index]);
        foreach (Transform child in gridPanel.transform.GetChild(index))
        {
            child.GetComponent<Image>().color = color;
        }
    }

    private Color GetMusicBarColor(int expected, int actual)
    {
        return actual < expected ? Color.gray : actual >= expected * 2 ? Color.yellow : Color.blue;
    }

    public void DrawMusicBar(bool fill = true)
    {
        var gridPanel = GetUIObject<RectTransform>("MusicBar");
        foreach (Transform child in gridPanel)
        {
            Destroy(child.gameObject); // 기존 블럭 제거
        }

        var currentChart = GameManager.Instance.CurrentChart;
        if (currentChart == null)
        {
            return;
        }

        var musicBar = currentChart.MusicBar;
        var musicBarScore = fill ? currentChart.MusicBarScore : new List<int>(new int[120]);
        for (var i = 0; i < musicBar.Count; ++i)
        {
            var color = GetMusicBarColor(musicBar[i], musicBarScore[i]);
            DrawRectangle(gridPanel, i * 11f, Math.Min(musicBar[i], 8), color);
        }
    }

    private void DrawRectangle(RectTransform panel, float x, int count, Color color)
    {
        GameObject barChild = new("BarGroup");
        barChild.transform.SetParent(panel);
        barChild.transform.localPosition = new(0, 0, 0);
        for (var i = 0; i < count; ++i)
        {
            // 배경을 위한 기본 이미지 생성
            GameObject rectObject = new("Rect");
            rectObject.transform.SetParent(barChild.transform);

            var image = rectObject.AddComponent<Image>();
            image.color = color;

            var rectTransform = rectObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new(10f, 10f); // 크기 설정
            rectTransform.anchoredPosition = new Vector2(x, i * 11f); // 위치 설정
        }
    }
}
