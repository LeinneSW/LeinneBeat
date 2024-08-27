using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Marker : MonoBehaviour
{
    public static readonly int STATE_PREFECT = 1;
    public static readonly int STATE_GREAT = 2;
    public static readonly int STATE_GOOD = 3;
    public static readonly int STATE_POOR = 4;
    public static readonly int STATE_MISS = 5;

    private Text text;
    private Animator animator;
    private HoldArrow arrowObject = null;
    private GameObject clickEffect = null;

    private float aliveTime = 0;
    public bool IsAlive { get => aliveTime > 0; }

    public GameObject ArrowGuide { get; private set; }
    public GameObject JudgeObject { get; private set; } = null;
    public float StartTime { get; private set; } = 0f;
    public float FinishTime { get; private set; } = 0f;
    public bool IsTouched { get; private set; } = false;

    private void Start()
    {
        animator = GetComponent<Animator>();
        text = transform.GetChild(0).GetChild(0).GetComponent<Text>();

        ArrowGuide = new GameObject("ArrowGuide");
        ArrowGuide.transform.SetParent(transform);
        ArrowGuide.transform.position = transform.position;
        ArrowGuide.SetActive(false);

        var arrowRenderer = ArrowGuide.AddComponent<SpriteRenderer>();
        arrowRenderer.sprite = MarkerManager.Instance.arrowSprite;
        arrowRenderer.sortingOrder = 6;

        clickEffect = new GameObject("ClickEffect");
        clickEffect.transform.SetParent(transform);
        clickEffect.transform.position = transform.position;
        clickEffect.SetActive(false);

        var clickRenderer = clickEffect.AddComponent<SpriteRenderer>();
        clickRenderer.sprite = MarkerManager.Instance.clickSprite;
        clickRenderer.sortingOrder = 7;
    }

    public void Show(Note note)
    {
        StartTime = (float)(note.StartTime + GameManager.Instance.StartTime);
        if (note.IsLong)
        {
            FinishTime = (float)(note.FinishTime + 29 / 60.0 + GameManager.Instance.StartTime);
            CreateArrow(note);
        }
        aliveTime = 23 / 30f; // 노트 출력 프레임수만큼만 판정을 유지
        animator.SetBool("Show", true);
    }

    private void Hide()
    {
        animator.SetBool("Show", false); // 마커 숨기기
    }

    private void CreateArrow(Note note)
    {
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
        arrowObject.duration = FinishTime;

        ArrowGuide.SetActive(true);
        ArrowGuide.transform.rotation = rotation;
    }

    public void OnTouch()
    {
        if (IsTouched)
        {
            return;
        }

        IsTouched = true;
        clickEffect.SetActive(true);
        if (IsAlive && JudgeObject == null)  // 판정을 낸적이 없고 살아있을경우에만 클릭이벤트 활성화
        {
            CalculateJudgement();
            if (arrowObject != null) // 롱노트의 경우
            {
                arrowObject.duration -= Time.time;
                arrowObject.EnableArrow();
            }
            else
            {
                StartTime = 0;
            }
            aliveTime = 0f; // 죽어있는 상태
            Hide();
        }
    }

    public void OnRelease()
    {
        if (!IsTouched)
        {
            return;
        }

        IsTouched = false;
        clickEffect.SetActive(false);
        CalculateJudgement(true);
    }

    private void HideJudgeText()
    {
        text.text = "";
    }

    public void CalculateJudgement(bool released = false)
    {
        if(released && (arrowObject == null || !arrowObject.IsStarted))
        {
            return;
        }

        var judge = STATE_POOR;
        var judgeTime = (released ? (FinishTime - Time.time) : (StartTime + 0.48333f - Time.time)) * 1000; // + 빠르게침, - 느리게침
        var judgeAbs = Mathf.Abs(judgeTime);
        if (judgeAbs <= 41.667f)
        {
            judge = STATE_PREFECT;
        }
        else if (judgeAbs <= 83.334f)
        {
            judge = STATE_GREAT;
        }
        else if (judgeAbs <= 166.667f)
        {
            judge = STATE_GOOD;
        }
        CreateJudgeEffect(judge, judgeTime);
        GameManager.Instance.AddScore(judge - 1);

        if (released)
        {
            Hide();
            StartTime = FinishTime = 0f;
            Destroy(arrowObject.gameObject);
        }
    }

    private void CreateJudgeEffect(int judge, float judgeTime)
    {
        if (judge == STATE_MISS)
        {
            return;
        }

        JudgeObject = Instantiate(MarkerManager.Instance.judgePrefab, transform.position, Quaternion.identity);
        Destroy(JudgeObject, 0.5333f);

        var animator = JudgeObject.GetComponent<Animator>();
        animator.SetInteger("State", judge);

        var judgeInt = Mathf.FloorToInt(judgeTime);
        if (judgeInt > 1)
        {
            text.color = new Color(14 / 255f, 61 / 255f, 130 / 255f);
            text.text = "+" + judgeInt;
        }
        else if (judgeInt < -1)
        {
            text.color = new Color(205 / 255f, 9 / 255f, 0);
            text.text = "" + judgeInt;
        }
        Invoke(nameof(HideJudgeText), 0.45f);
    }

    private void DisableTouchHack()
    {
        // 자동 재생 모드에서만 사용되는 함수입니다
        clickEffect.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance.AutoMode)
        {
            if (StartTime > 0 && Time.time >= StartTime + 0.4825f)
            {
                IsTouched = false;
                OnTouch();
                Invoke(nameof(DisableTouchHack), FinishTime <= 0 ? 0.16f : FinishTime - Time.time + 0.01f);
            }
            return;
        }

        aliveTime -= Time.deltaTime;
        if (IsAlive || !animator.GetBool("Show"))
        {
            return;
        }

        Hide();
        if (JudgeObject == null) // 노트를 입력하지 않았을 경우
        {
            if (arrowObject != null) // 롱노트는 2미스
            {
                Destroy(arrowObject.gameObject);
                GameManager.Instance.AddScore(STATE_MISS - 1);
            }
            StartTime = FinishTime = 0f;
            GameManager.Instance.AddScore(STATE_MISS - 1);
        }
    }
}
