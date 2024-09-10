using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private readonly Dictionary<string, Component> componentCache = new();

    public GameObject SettingPane;
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

        var foundObject = GameObject.Find(name)?.GetComponent<T>();
        if (foundObject == null)
        {
            Debug.LogWarning($"UI Object with name '{name}' not found.");
            return null;
        }
        componentCache[name] = foundObject;
        return foundObject;
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

    public void SortMusicButton()
    {
        MusicManager.Instance.MusicList.Sort((x, y) => string.Compare(x.Title, y.Title, StringComparison.Ordinal));
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

    public void InitSelectMusicScene()
    {
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

        var sortByName = GetUIObject<Button>("SortByName");
        sortByName.onClick.AddListener(SortMusicButton);
        //var settingButton = GetUIObject<Button>("SettingButton");
    }

    public void ToggleSetting()
    {
        SettingPane.SetActive(!SettingPane.activeSelf);
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
            case Difficulty.Extreme:
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
                break;
            case GameManager.SceneInGame:
                DrawMusicBar();

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

    public void UpdateMusicBar(int index)
    {
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

    public void DrawMusicBar()
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
        var musicBarScore = currentChart.MusicBarScore;
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
