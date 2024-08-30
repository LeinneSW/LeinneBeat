using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } = null;

    private Dictionary<string, Component> componentCache = new Dictionary<string, Component>();

    public GameObject musicButton;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public T GetUIObject<T>(string name) where T : Component
    {
        if (componentCache.TryGetValue(name, out var cacheComponent) && cacheComponent != null)
        {
            return cacheComponent as T;
        }

        T foundObject = GameObject.Find(name)?.GetComponent<T>();
        if (foundObject == null)
        {
            Debug.LogWarning($"UI Object with name '{name}' not found.");
            return null;
        }
        componentCache[name] = foundObject;
        return foundObject;
    }

    private void AddMusicButton(RectTransform content, Music music)
    {
        // TODO: 버튼 에셋 추가 예정
        var button = Instantiate(musicButton, content);
        button.GetComponent<Button>().onClick.AddListener(() => GameManager.Instance.SelectMusic(music));
        button.GetComponentInChildren<Text>().text = music.name;
    }

    public void InitSelectMusicScene()
    {
        GetUIObject<Button>("StartGameButton").onClick.AddListener(() => GameManager.Instance.PlayChart());
        var content = GetUIObject<RectTransform>("MusicListContent");
        for (int i = 0; i < GameManager.Instance.musicList.Count; ++i)
        {
            Debug.Log($"곡 이름: {GameManager.Instance.musicList[i].name}");
            AddMusicButton(content, GameManager.Instance.musicList[i]);
        }
        var basic = GetUIObject<Button>("BasicButton");
        var advanced = GetUIObject<Button>("AdvancedButton");
        var extreme = GetUIObject<Button>("ExtremeButton");
        basic.onClick.AddListener(() => {
            // TODO: Basic sound
            GameManager.Instance.SelectedDifficulty = Difficulty.Basic;
        });
        advanced.onClick.AddListener(() => {
            // TODO: Advnaced sound
            GameManager.Instance.SelectedDifficulty = Difficulty.Advanced;
        });
        extreme.onClick.AddListener(() => {
            // TODO: Extreme sound
            GameManager.Instance.SelectedDifficulty = Difficulty.Extreme;
        });
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case GameManager.SCENE_MUSIC_SELECT:
                InitSelectMusicScene();
                break;
            case GameManager.SCENE_IN_GAME:
                var titleText = GetUIObject<Text>("MusicTitle");
                titleText.text = GameManager.Instance.SelectedMusic.name;

                var offsetText = GetUIObject<InputField>("MusicOffset");
                offsetText.text = "" + GameManager.Instance.SelectedMusic.StartOffset;
                offsetText.onValueChanged.AddListener((value) =>
                {
                    // BUG: 입력시 다시 값이 변경되지 않도록(0. >> 0이되어버림)
                    if (float.TryParse(value, out var offset))
                    {
                        GameManager.Instance.SetMusicOffset(offset);
                    }
                });
                for (int i = 1; i < 4; ++i)
                {
                    var number = 1 / Math.Pow(10, i);
                    var plusButton = GetUIObject<Button>("+" + number);
                    plusButton.onClick.AddListener(() => GameManager.Instance.AddMusicOffset((float)number));

                    var minusButton = GetUIObject<Button>("-" + number);
                    minusButton.onClick.AddListener(() => GameManager.Instance.AddMusicOffset((float)-number));
                }

                var autoButton = GetUIObject<Button>("AutoButton");
                autoButton.onClick.AddListener(() => {
                    GameManager.Instance.AutoMode = !GameManager.Instance.AutoMode;
                    autoButton.GetComponentInChildren<Text>().text = "현재: " + (GameManager.Instance.AutoMode ? "On" : "Off");
                });
                break;
        }
    }

    public void DrawRectangle(RectTransform parent, Vector2 position)
    {
        // 배경을 위한 기본 이미지 생성
        GameObject rectObject = new("Rect");
        rectObject.transform.SetParent(parent);

        var image = rectObject.AddComponent<Image>();
        image.color = Color.gray;

        // RectTransform 설정
        RectTransform rectTransform = rectObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new(9f, 9f); // 크기 설정
        rectTransform.anchoredPosition = position - new Vector2(650, 55) + new Vector2(11 / 2f, 11 / 2f); // 위치 설정
    }
}
