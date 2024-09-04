using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private readonly Dictionary<string, Component> componentCache = new();

    public GameObject MusicButton;

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
        var button = Instantiate(MusicButton, content);
        button.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.SelectMusic(music));
        button.transform.GetChild(0).GetComponent<Text>().text = $"{music.Title}{(music.IsLong ? " (홀드)" : "")}";
        button.transform.GetChild(1).GetComponent<Text>().text = music.Author;
        if (music.Jacket != null)
        {
            button.transform.GetChild(2).GetComponent<Image>().sprite = music.Jacket;
        }
    }

    public void ResetMusicList()
    {
        var content = GetUIObject<RectTransform>("MusicListContent");
        foreach (Transform child in content)
        {
            Destroy(child.gameObject); // 기존 버튼 제거
        }
    }

    public void InitSelectMusicScene()
    {
        GetUIObject<Button>("StartGameButton").onClick.AddListener(() => GameManager.Instance.PlayMusic());
        var basic = GetUIObject<Button>("BasicButton");
        basic.onClick.AddListener(() => GameManager.Instance.SelectDifficulty(Difficulty.Basic));
        var advanced = GetUIObject<Button>("AdvancedButton");
        advanced.onClick.AddListener(() => GameManager.Instance.SelectDifficulty(Difficulty.Advanced));
        var extreme = GetUIObject<Button>("ExtremeButton");
        extreme.onClick.AddListener(() => GameManager.Instance.SelectDifficulty(Difficulty.Extreme));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case GameManager.SceneMusicSelect:
                InitSelectMusicScene();
                break;
            case GameManager.SceneInGame:
                var titleText = GetUIObject<Text>("MusicTitle");
                titleText.text = GameManager.Instance.CurrentMusic.Title;

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

    public void DrawMusicBar(List<int> musicBar)
    {
        var gridPanel = GetUIObject<RectTransform>("MusicBar");
        foreach (Transform child in gridPanel)
        {
            Destroy(child.gameObject); // 기존 블럭 제거
        }

        for (var i = 0; i < musicBar.Count; ++i)
        {
            for (int j = 0, limit = Math.Min(musicBar[i], 8); j < limit; ++j)
            {
                UIManager.Instance.DrawRectangle(gridPanel, new(i * 11f, j * 11f));
            }
        }
    }

    private void DrawRectangle(RectTransform parent, Vector2 position)
    {
        // 배경을 위한 기본 이미지 생성
        GameObject rectObject = new("Rect");
        rectObject.transform.SetParent(parent);

        var image = rectObject.AddComponent<Image>();
        image.color = Color.gray;

        var rectTransform = rectObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new(10f, 10f); // 크기 설정
        rectTransform.anchoredPosition = position; // 위치 설정
    }
}
