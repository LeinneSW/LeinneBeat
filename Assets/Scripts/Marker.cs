using System;
using UnityEngine;

public enum JudgeState
{
    Perfect,
    Great,
    Good,
    Poor,
    Miss,
}

public class Marker : MonoBehaviour
{
    private bool touched;

    private Animator animator; // hold
    private HoldArrow arrowObject; // hold
    private GameObject judgeObject;
    private double remainTime = 23 / 30f; // 마커의 이미지는 총 23장

    public Note Note;

    public GameObject ArrowGuide { get; private set; }
    /**
     * 노트의 소환시간을 나타냅니다
     * 노트의 정확한 perfect 타이밍은 StartTime + 29 / 60(14.5fps)입니다.
     */
    public double StartTime { get; private set; }
    /**
     * 노트의 종료시간을 나타냅니다
     * 노트가 완전히 종료되어 사라지게 되는 시간입니다.
     */
    public double FinishTime { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.enabled = false;

        ArrowGuide = new GameObject("ArrowGuide");
        ArrowGuide.transform.SetParent(transform);
        ArrowGuide.transform.position = transform.position;
        ArrowGuide.SetActive(false);

        var arrowRenderer = ArrowGuide.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = MarkerManager.Instance.arrowSprite;
        arrowRenderer.sortingOrder = 6;
    }

    private void Start()
    {
        StartTime = Note.StartTime + GameManager.Instance.StartTime;
        CreateArrow();
    }

    private void CreateArrow()
    {
        if (Note.FinishTime <= 0)
        {
            return;
        }

        var rotate = 0;
        var colDiff = Note.BarColumn - Note.Column;
        rotate = colDiff switch
        {
            > 0 => 270, // []---<
            < 0 => 90, // >---[]
            _ => rotate
        };
        var rowDiff = Note.BarRow - Note.Row;
        if (rowDiff > 0)
        {
            // [ ] 
            //  |
            //  |
            //  ∧
            rotate = 180;
        }
        var rotation = Quaternion.Euler(0, 0, rotate);

        arrowObject = Instantiate(MarkerManager.Instance.arrowPrefab, Note.BarPosition, rotation).GetComponent<HoldArrow>();
        arrowObject.gameObject.transform.SetParent(transform);

        arrowObject.Duration = FinishTime = Note.FinishTime + GameManager.Instance.StartTime + 29 / 60d;

        ArrowGuide.SetActive(true);
        ArrowGuide.transform.rotation = rotation;
    }

    private void Destroy()
    {
        Destroy(gameObject);
    }

    private void EnableHoldAnimation()
    {
        animator.enabled = true;
        animator.SetBool("Hold", true);
    }

    public void OnTouch(double touchTime)
    {
        if (touched)
        {
            return;
        }

        touched = true;
        var judge = JudgeState.Poor; // 입력이 들어갔으면 최소한 poor
        var judgeTime = StartTime - touchTime + 29 / 60d; // 빠르게(+), 느리게(-)
        var judgeAbs = Math.Abs(judgeTime);
        var judgeTable = MarkerManager.Instance.CurrentJudgementTable;
        if (judgeAbs <= judgeTable[0])
        {
            judge = JudgeState.Perfect;
        }
        else if (judgeAbs <= judgeTable[1])
        {
            judge = JudgeState.Great;
        }
        else if (judgeAbs <= judgeTable[2])
        {
            judge = JudgeState.Good;
        }
        CreateJudgeEffect(judge, touchTime);
        GameManager.Instance.AddScore(judge, Note.MusicBarIndex, judgeTime > 0);
        MarkerManager.Instance.ShowJudgeText(Note.Row, Note.Column, judge, judgeTime);
        if (arrowObject == null)
        { // 롱노트가 아닌 경우
            remainTime = 0.2f;
            Invoke(nameof(Destroy), 0.06f);
        }
        else
        {
            if (judge != JudgeState.Poor)
            {
                arrowObject.Duration -= Time.timeAsDouble;
                remainTime = arrowObject.Duration + 0.166;
                arrowObject.EnableArrow();
                Invoke(nameof(EnableHoldAnimation), 16f / 30f); // 판정 이펙트 종료 후 시작
            }
            else
            { // TODO: poor 판정의 롱노트 방식이 어떻게 되는지 파악하지 못함
                remainTime = 0.2f;
                Invoke(nameof(Destroy), 0.06f);
                GameManager.Instance.AddScore(JudgeState.Poor, Note.MusicBarLongIndex);
            }
        }
    }

    public void OnRelease()
    {
        OnRelease(FinishTime);
    }

    public void OnRelease(double releaseTime)
    {
        if (!touched || arrowObject == null)
        { // 노트를 누르지 않았거나 롱노트가 아닌 경우는 무시
            return;
        }
        var judge = JudgeState.Poor; // 입력이 들어갔으면 최소한 poor
        var judgeTime = Math.Max(0, FinishTime - releaseTime); // 롱노트에는 빠름(+) 판정만 존재함
        var judgeTable = MarkerManager.Instance.CurrentJudgementTable;
        // TODO: 롱노트의 판정을 아직 파악하지 못함
        if (judgeTime <= judgeTable[0])
        {
            judge = JudgeState.Perfect;
        }
        else if (judgeTime <= judgeTable[1])
        {
            judge = JudgeState.Great;
        }
        else if (judgeTime <= judgeTable[2])
        {
            judge = JudgeState.Good;
        }
        CreateJudgeEffect(judge, releaseTime);
        GameManager.Instance.AddScore(judge, Note.MusicBarLongIndex, judgeTime > 0);

        Destroy(arrowObject.gameObject);
        Destroy(gameObject);
    }

    private void CreateJudgeEffect(JudgeState judge, double touchTime)
    {
        if (judge == JudgeState.Miss)
        {
            return;
        }

        if (judgeObject != null)
        {
            Destroy(judgeObject);
        } 
        judgeObject = Instantiate(MarkerManager.Instance.judgePrefab, transform.position, Quaternion.identity);
        Destroy(judgeObject, 0.5333f);

        var animator = judgeObject.GetComponent<MarkerAnimator>();
        animator.StartTime = touchTime;
        animator.SpriteList = MarkerManager.CurrentMarkerSprites[(int)judge + 1];
    }

    private void Update()
    {
        if (GameOptions.Instance.AutoPlay)
        {
            if (Time.time >= StartTime + 29 / 60d - Time.deltaTime)
            { // 프레임타임 기준으로 판단하도록 개선
                OnTouch(StartTime + 29 / 60d);
            }
            return;
        }

        remainTime -= Time.deltaTime;
        if (remainTime > 0) return; // 입력하지 않은 경우
        if (arrowObject != null) // 롱노트는 2미스
        {
            Destroy(arrowObject.gameObject);
            GameManager.Instance.AddScore(JudgeState.Miss, Note.MusicBarLongIndex);
        }
        GameManager.Instance.AddScore(JudgeState.Miss, Note.MusicBarIndex);
        Destroy(gameObject);
    }
}
