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
    private bool touched = false;

    private float remainTime = 23 / 30f;
    private Animator animator; // hold
    private HoldArrow arrowObject; // hold
    private GameObject judgeObject;

    public Note Note;

    public GameObject ArrowGuide { get; private set; }
    public float StartTime { get; private set; }
    public float FinishTime { get; private set; }

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

        StartTime = (float)(Note.StartTime + GameManager.Instance.StartTime);
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

        FinishTime = (float)(Note.FinishTime + GameManager.Instance.StartTime);
        arrowObject.Duration = FinishTime + 29 / 60f;

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
            arrowObject.Duration -= Time.time;
            remainTime = arrowObject.Duration + 0.166f;
            arrowObject.EnableArrow();
            Invoke(nameof(EnableHoldAnimation), 16f / 30f);
        }
    }

    public void CalculateJudgement()
    {
        var judge = JudgeState.Poor;
        var judgeTime = StartTime + 29 / 60d - Time.timeAsDouble; // + 빠르게침, - 느리게침
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
        CreateJudgeEffect(judge);
        GameManager.Instance.AddScore((int) judge);
        MarkerManager.Instance.ShowJudgeTime(Note.Row, Note.Column, judgeTime * 1000);
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
        var judge = JudgeState.Poor;
        var judgeTime = Math.Max(0, FinishTime + 29 / 60d - Time.timeAsDouble); // + 빠르게침, - 판정은 없음
        //var maxTime = FinishTime - StartTime;
        var judgeTable = MarkerManager.Instance.CurrentJudgementTable;
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
        CreateJudgeEffect(judge);
        GameManager.Instance.AddScore((int) judge);

        Destroy(gameObject);
        Destroy(arrowObject.gameObject);
    }

    private void CreateJudgeEffect(JudgeState judge)
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

        var animator = judgeObject.GetComponent<Animator>();
        animator.SetInteger("State", (int) judge + 1);
    }

    private void Update()
    {
        if (GameManager.Instance.AutoPlay)
        {
            if (Time.time > StartTime + 0.4815f)
            {
                OnTouch();
            }
            return;
        }

        remainTime -= Time.deltaTime;
        if (remainTime > 0) return; // 입력하지 않은 경우
        if (arrowObject != null) // 롱노트는 2미스
        {
            Destroy(arrowObject.gameObject);
            GameManager.Instance.AddScore((int) JudgeState.Miss - 1);
        }
        GameManager.Instance.AddScore((int) JudgeState.Miss - 1);
        Destroy(gameObject);
    }
}