using System;
using UnityEngine;

public class MarkerObject : MonoBehaviour
{
    public static readonly int STATE_PREFECT = 1;
    public static readonly int STATE_GREAT = 2;
    public static readonly int STATE_GOOD = 3;
    public static readonly int STATE_POOR = 4;
    public static readonly int STATE_MISS = 5;

    private bool touched = false;

    private float remainTime = 23 / 30f;
    private Animator animator; // hold
    private HoldArrow arrowObject = null; // hold
    private GameObject judgeObject = null;

    public Note note;

    public GameObject ArrowGuide { get; private set; }
    public float StartTime { get; private set; } = 0f;
    public float FinishTime { get; private set; } = 0f;

    private void Start()
    {
        animator = GetComponent<Animator>();

        ArrowGuide = new GameObject("ArrowGuide");
        ArrowGuide.transform.SetParent(transform);
        ArrowGuide.transform.position = transform.position;
        ArrowGuide.SetActive(false);

        var arrowRenderer = ArrowGuide.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = MarkerManager.Instance.arrowSprite;
        arrowRenderer.sortingOrder = 6;

        StartTime = (float)(note.StartTime + GameManager.Instance.StartTime);
        CreateArrow();
    }

    private void CreateArrow()
    {
        if (note.FinishTime <= 0)
        {
            return;
        }

        int rotate = 0;
        var colDiff = note.BarColumn - note.Column;
        if (colDiff > 0)
        {
            // []---<
            rotate = 270;
        }
        else if (colDiff < 0)
        {
            // >---[]
            rotate = 90;
        }
        var rowDiff = note.BarRow - note.Row;
        if (rowDiff > 0)
        {
            // [ ] 
            //  |
            //  |
            //  ∧
            rotate = 180;
        }
        var rotation = Quaternion.Euler(0, 0, rotate);

        arrowObject = Instantiate(MarkerManager.Instance.arrowPrefab, note.BarPosition, rotation).GetComponent<HoldArrow>();
        arrowObject.gameObject.transform.SetParent(transform);

        FinishTime = (float)(note.FinishTime + GameManager.Instance.StartTime);
        arrowObject.duration = FinishTime + 29 / 60f;

        ArrowGuide.SetActive(true);
        ArrowGuide.transform.rotation = rotation;
    }

    private void Destroy()
    {
        Destroy(gameObject);
    }

    private void EnableHoldAnimation()
    {
        animator.SetBool("Hold", true);
    }

    public void OnTouch()
    {
        if (touched)
        {
            return;
        }

        touched = true;
        CalculateJudgement();
        if (arrowObject == null)
        {
            remainTime = 0.2f;
            Invoke(nameof(Destroy), 0.06f);
        }
        else // 롱노트의 경우
        {
            arrowObject.duration -= Time.time;
            remainTime = arrowObject.duration + 0.166f;
            arrowObject.EnableArrow();
            Invoke(nameof(EnableHoldAnimation), 16f / 30f);
        }
    }

    public void CalculateJudgement()
    {
        var judge = STATE_POOR;
        var judgeTime = (StartTime + 29 / 60d - Time.time) * 1000; // + 빠르게침, - 느리게침
        var judgeAbs = Math.Abs(judgeTime);
        // TODO: 판정 범위 조절 기능 구현 예정
        if (judgeAbs <= 41.667f)
        {
            judge = STATE_PREFECT;
        }
        else if (judgeAbs <= 83.334f)
        {
            judge = STATE_GREAT;
        }
        else if (judgeAbs <= 125f)
        {
            judge = STATE_GOOD;
        }
        GameManager.Instance.AddScore(judge - 1);
        CreateJudgeEffect(judge);
        MarkerManager.Instance.ShowJudgeTime(note.Row, note.Column, judgeTime);
    }

    public void OnRelease()
    {
        if (arrowObject == null || !arrowObject.IsStarted) // 롱노트를 누르기 전이거나 롱노트가 아닌 경우는 무시
        {
            return;
        }
        CalculateJudgementRelease();
    }

    private void CalculateJudgementRelease()
    {
        // TODO: 롱노트의 판정 산정 방식은 다르게 측정되어야함
        var judge = STATE_POOR;
        var judgeTime = Math.Max(0, (FinishTime + 29 / 60d - Time.time) * 1000); // + 빠르게침, - 판정은 없음
        //var maxTime = FinishTime - StartTime;
        if (judgeTime <= 41.667f)
        {
            judge = STATE_PREFECT;
        }
        else if (judgeTime <= 83.334f)
        {
            judge = STATE_GREAT;
        }
        else if (judgeTime <= 166.667f)
        {
            judge = STATE_GOOD;
        }
        CreateJudgeEffect(judge);
        GameManager.Instance.AddScore(judge - 1);

        Destroy(gameObject);
        Destroy(arrowObject.gameObject);
    }

    private void CreateJudgeEffect(int judge)
    {
        if (judge == STATE_MISS)
        {
            return;
        }

        if (judgeObject != null)
        {
            Destroy(judgeObject);
        } 
        judgeObject = Instantiate(MarkerManager.Instance.judgePrefab, transform.position, Quaternion.identity);
        Destroy(judgeObject, 0.5333f);

        var animator = judgeObject.GetComponent<Animator>();
        animator.SetInteger("State", judge);
    }

    void Update()
    {
        if (GameManager.Instance.AutoMode)
        {
            if (Time.time > StartTime + 0.482f)
            {
                OnTouch();
            }
            return;
        }

        remainTime -= Time.deltaTime;
        if (remainTime > 0)
        {
            return;
        }

        if (arrowObject != null) // 롱노트는 2미스
        {
            Destroy(arrowObject.gameObject);
            GameManager.Instance.AddScore(STATE_MISS - 1);
        }
        GameManager.Instance.AddScore(STATE_MISS - 1);
        Destroy(gameObject);
    }
}
