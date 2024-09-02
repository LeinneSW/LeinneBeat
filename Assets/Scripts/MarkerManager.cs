using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum JudgementType
{
    Normal,
    Hard,
    Extreme
}

public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; } = null;

    public static JudgementType JudgeType { get; private set; } = JudgementType.Normal;
    public static readonly Dictionary<JudgementType, double[]> judgementTables = new()
    {
        { JudgementType.Normal, new double[] { 2.5 / 60, 5 / 60, 7.5 / 60 } },
        { JudgementType.Hard, new double[] { 2.5 / 45, 5 / 45, 7.5 / 45 } },
        { JudgementType.Extreme, new double[] { 2.5 / 30, 5 / 30, 7.5 / 30 } },
    }
    public static double[] CurrentJudgementTable
    {
        get => judgementTables[JudgeType];
    }

    public GameObject judgePrefab;
    public GameObject markerPrefab;

    public Sprite clickSprite;
    public Sprite arrowSprite;
    public AudioClip clapSound;
    public GameObject arrowPrefab;
    public GameObject judgeTextPrefab;

    private readonly List<Text> judgeText = new();
    private readonly List<AudioSource> claps = new();
    private readonly List<GameObject> touchedList = new();
    private readonly Dictionary<int, List<MarkerObject>> markers = new();

    private int _clapIndex = 0;
    private int ClapIndex
    {
        get => _clapIndex;
        set => _clapIndex = value > 15 ? 0 : value;
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        var canvas = GameObject.Find("JudgeCanvas");
        for (int row = 0; row < 4; ++row)
        {
            for (int column = 0; column < 4; ++column)
            {
                markers[row * 4 + column] = new();
                var textObj = Instantiate(judgeTextPrefab, ConvertPosition(row, column), Quaternion.identity);
                textObj.transform.SetParent(canvas.transform);
                judgeText.Add(textObj.GetComponentInChildren<Text>());

                var clickEffect = new GameObject("ClickEffect");
                clickEffect.transform.SetParent(transform);
                clickEffect.transform.position = ConvertPosition(row, column);
                clickEffect.SetActive(false);
                touchedList.Add(clickEffect);

                var clickRenderer = clickEffect.AddComponent<SpriteRenderer>();
                clickRenderer.sprite = clickSprite;
                clickRenderer.sortingOrder = 7;

                var audio = gameObject.AddComponent<AudioSource>();
                audio.loop = false;
                audio.playOnAwake = false;
                audio.clip = clapSound;
                claps.Add(audio);
            };
        }
    }

    private void Start()
    {
        for (int i = 0; i < 16; ++i)
        {
            claps[i].volume = GameManager.Instance.ClapVolume;
        }
    }

    public Vector3 ConvertPosition(int row, int column)
    {
        return new(column * 400 - 600, 120 - row * 400);
    }

    public void ShowMarker(Note note)
    {
        var marker = Instantiate(markerPrefab, note.Position, Quaternion.identity).GetComponent<MarkerObject>();
        marker.note = note;
        markers[note.Row * 4 + note.Column].Add(marker);
    }

    public void ToggleClapSound()
    {
        GameManager.Instance.ClapVolume = GameManager.Instance.ClapVolume > 0 ? 0 : 0.5f;
        foreach (var clap in claps)
        {
            clap.volume = GameManager.Instance.ClapVolume;
        }
    }

    public void PlayClap()
    {
        claps[ClapIndex++].Play();
    }

    private IEnumerator ShowJudgeTime(Text text, int judge)
    {
        if (judge > 1)
        {
            text.color = new Color(14 / 255f, 61 / 255f, 130 / 255f);
            text.text = "+" + judge;
        }
        else if (judge < -1)
        {
            text.color = new Color(205 / 255f, 9 / 255f, 0);
            text.text = "" + judge;
        }
        yield return new WaitForSeconds(16f / 30);
        text.text = "";
    }

    public void ShowJudgeTime(int row, int column, double judgeTime)
    {
        StartCoroutine(ShowJudgeTime(judgeText[row * 4 + column], (int)Math.Floor(judgeTime)));
    }

    private void OnTouch(int row, int column)
    {
        if (touchedList[row * 4 + column].activeSelf)
        {
            return;
        }
        touchedList[row * 4 + column].SetActive(true);

        var list = markers[row * 4 + column];
        for (int i = list.Count - 1; i >= 0; --i)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
        if (list.Count > 0)
        {
            list[0].OnTouch();
        }
    }

    private void OnRelease(int row, int column)
    {
        touchedList[row * 4 + column].SetActive(false);

        var list = markers[row * 4 + column];
        for (int i = list.Count - 1; i >= 0; --i)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
        if (list.Count > 0)
        {
            list[0].OnRelease();
        }
    }

    void Update()
    {
        if (GameManager.Instance.AutoMode)
        {
            return;
        }

        List<int> touched = new();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touchState = Input.GetTouch(i);
            Vector2 touchPosition = Camera.main.ScreenToWorldPoint(touchState.position);
            // row 320 ~ -1480
            // col -800 ~ 800
            touchPosition += new Vector2(800, -320);
            touchPosition /= 400;
            var row = Mathf.FloorToInt(-touchPosition.y);
            var column = Mathf.FloorToInt(touchPosition.x);
            if (0 <= row && row < 4 && 0 <= column && column < 4)
            {
                if (touchState.phase != TouchPhase.Ended)
                {
                    touched.Add(column + row * 4);
                }
            }
        }

        if (Input.touchCount < 1 && Input.GetMouseButton(0))
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition += new Vector2(800, -320);
            mousePosition /= 400;
            var row = Mathf.FloorToInt(-mousePosition.y);
            var column = Mathf.FloorToInt(mousePosition.x);
            if (0 <= row && row < 4 && 0 <= column && column < 4)
            {
                touched.Add(column + row * 4);
            }
        }

        for (int row = 0; row < 4; ++row)
        {
            for (int column = 0; column < 4; ++column)
            {
                if (touched.Contains(row * 4 + column))
                {
                    OnTouch(row, column);
                }
                else
                {
                    OnRelease(row, column);
                }
            }
        }
    }
}