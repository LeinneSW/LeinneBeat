using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MarkerAnimation
{
    public readonly double SampleRate;
    public readonly List<Sprite> SpriteList = new();

    public MarkerAnimation(List<Sprite> spriteList, int sampleRate = 30)
    {
        SpriteList = spriteList;
        SampleRate = sampleRate;
    }
}

public class MarkerManager : MonoBehaviour
{
    public static MarkerManager Instance { get; private set; }
    public static MarkerAnimation HoldAnimation { get; private set; };
    public static readonly Dictionary<string, MarkerAnimation> MarkerAnimationList = new();

    public double[] CurrentJudgementTable => judgementTables[GameOptions.Instance.JudgementType];

    public GameObject judgePrefab;
    public GameObject markerPrefab;
    public GameObject startHerePrefab;

    public Sprite clickSprite;
    public Sprite arrowSprite;
    public AudioClip clapSound;
    public GameObject arrowPrefab;
    public GameObject judgeTextPrefab;

    private readonly List<Text> judgeText = new();
    private readonly List<AudioSource> clapList = new();
    private readonly List<GameObject> touchedList = new();
    private readonly Dictionary<int, List<Marker>> markers = new();
    private readonly Dictionary<JudgementType, double[]> judgementTables = new()
    {
        { JudgementType.Normal, new[] { 2.5 / 60, 5.0 / 60, 7.5 / 60 } },
        { JudgementType.Hard, new[] { 2.5 / 90, 5.0 / 90, 7.5 / 90 } },
        { JudgementType.Extreme, new[] { 2.5 / 120, 5.0 / 120, 7.5 / 120 } },
    };

    private int clapIndex;
    private int ClapIndex
    {
        get => clapIndex;
        set => clapIndex = value > 15 ? 0 : value;
    }

    private static MarkerAnimation LoadMarkerAnimation(string basePath, string markerType)
    {
        // TODO: read sample rate
        var markerDirPath = Path.Combine(basePath, markerType);
        if (Directory.Exists(markerDirPath))
        {
            var files = Directory.GetFiles(markerDirPath, "*.png").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length > 0)
            {
                List<Sprite> spriteList = new();
                foreach (var file in files)
                {
                    var bytes = File.ReadAllBytes(file);
                    var texture = new Texture2D(2, 2);
                    texture.LoadImage(bytes);
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / 400f);
                    spriteList.Add(sprite);
                }
                return new MarkerAnimation(spriteList);
            }
        }
        Debug.LogWarning($"마커 애니메이션 폴더(`marker/{markerType}`)가 비어있거나 존재하지 않습니다.");
        return new MarkerAnimation(new());
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (MarkerAnimationList.Count > 0) return;

        var basePath = Path.Combine(Application.dataPath, "..", "Theme", "marker");
        MarkerAnimationList["normal"] = LoadMarkerAnimation(basePath, "normal");
        for(var i = 0; i < (int) JudgeState.Miss; ++i){
            var judge = (JudgeState) i;
            MarkerAnimationList[markerType] = LoadMarkerAnimation(basePath, judge.ToString());
        }
        HoldAnimation = LoadMarkerAnimation(basePath, "hold");
    }

    private void Start()
    {
        var canvas = GameObject.Find("JudgeCanvas");
        for (var row = 0; row < 4; ++row)
        {
            for (var column = 0; column < 4; ++column)
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

                var clap = gameObject.AddComponent<AudioSource>();
                clap.loop = false;
                clap.clip = clapSound;
                clap.playOnAwake = false;
                clap.volume = GameOptions.Instance.ClapVolume;
                clapList.Add(clap);
            };
        }

    }

    public Vector3 ConvertPosition(int row, int column)
    {
        return new(column * 400 - 600, 120 - row * 400);
    }

    public void ShowMarker(Note note)
    {
        var markerObj = Instantiate(markerPrefab, note.Position, Quaternion.identity);
        var marker = markerObj.GetComponent<Marker>();
        marker.Note = note;
        markers[note.Row * 4 + note.Column].Add(marker);

        var markerAnimator = markerObj.GetComponent<MarkerAnimator>();
        markerAnimator.Animation = MarkerAnimationList["normal"];
        markerAnimator.StartTime = note.StartTime + GameManager.Instance.StartTime;
    }

    public void PlayClap()
    {
        clapList[ClapIndex++].Play();
    }

    private IEnumerator ShowJudgeText(Text text, int judge)
    {
        switch (judge)
        {
            case > 1:
                text.color = new Color(14 / 255f, 61 / 255f, 130 / 255f);
                text.text = "+" + judge;
                break;
            case < -1:
                text.color = new Color(205 / 255f, 9 / 255f, 0);
                text.text = "" + judge;
                break;
        }
        yield return new WaitForSeconds(16f / 30);
        text.text = "";
    }

    public void ShowJudgeText(int row, int column, JudgeState judge, double judgeTime)
    {
        switch (GameOptions.Instance.JudgementDisplay)
        {
            case JudgementDisplay.None:
                return;
            case JudgementDisplay.Great:
                if(judge == JudgeState.Perfect) return;
                break;
        }
        StartCoroutine(ShowJudgeText(judgeText[row * 4 + column], (int)Math.Round(judgeTime * 1000)));
    }

    private void OnTouch(int row, int column, double touchTime)
    {
        if (touchedList[row * 4 + column].activeSelf)
        {
            return;
        }
        touchedList[row * 4 + column].SetActive(true);

        var list = markers[row * 4 + column];
        for (var i = list.Count - 1; i >= 0; --i)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
        if (list.Count > 0)
        {
            list[0].OnTouch(touchTime);
        }
    }

    private void OnRelease(int row, int column, double releaseTime)
    {
        touchedList[row * 4 + column].SetActive(false);

        var list = markers[row * 4 + column];
        for (var i = list.Count - 1; i >= 0; --i)
        {
            if (list[i] == null)
            {
                list.RemoveAt(i);
            }
        }
        if (list.Count > 0)
        {
            list[0].OnRelease(releaseTime);
        }
    }

    private void Update()
    {
        if (!GameManager.Instance.IsStarted || GameOptions.Instance.AutoPlay)
        {
            return;
        }

        var touchTime = Time.timeAsDouble;
        Dictionary<int, bool> touchData = new();
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePosition += new Vector2(800, -320);
            mousePosition /= 400;
            var row = Mathf.FloorToInt(-mousePosition.y);
            var column = Mathf.FloorToInt(mousePosition.x);
            if (row is >= 0 and < 4 && column is >= 0 and < 4)
            {
                touchData[column + row * 4] = true;
            }
        }
#else
        for (var i = 0; i < Input.touchCount; i++)
        {
            var touchState = Input.GetTouch(i);
            if (touchState.phase == TouchPhase.Ended) continue;

            Vector2 touchPosition = Camera.main.ScreenToWorldPoint(touchState.position);
            // row 320 ~ -1480
            // col -800 ~ 800
            touchPosition += new Vector2(800, -320);
            touchPosition /= 400;
            var row = Mathf.FloorToInt(-touchPosition.y);
            var column = Mathf.FloorToInt(touchPosition.x);
            touchData[column + row * 4] = true;
        }
#endif

        for (var row = 0; row < 4; ++row)
        {
            for (var column = 0; column < 4; ++column)
            {
                if (touchData.ContainsKey(column + row * 4))
                {
                    OnTouch(row, column, touchTime);
                }
                else
                {
                    OnRelease(row, column, touchTime);
                }
            }
        }
    }
}