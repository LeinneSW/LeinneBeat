using System.Collections;
using UnityEngine;

public class HoldArrow : MonoBehaviour
{
    private MarkerObject marker;
    private LineRenderer lineRenderer;
    private RectTransform rectTransform;

    public float duration = 0;

    public bool IsStarted { get; private set; } = false;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.widthMultiplier = 22;
        lineRenderer.SetPosition(0, transform.position);
        lineRenderer.SetPosition(1, transform.position);
        rectTransform = GetComponent<RectTransform>();
        SetScale(new(0, .4f, 1));
    }

    private void Start()
    {
        var rotate = transform.rotation.eulerAngles.z;
        Vector3 offset = rotate switch
        {
            270 => new(-200, 0, 0), // <
            90 => new(200, 0, 0), // >
            180 => new(0, 200, 0), // ∧
            _ => new(0, -200, 0), // V
        };
        marker = transform.GetComponentInParent<MarkerObject>();
        lineRenderer.SetPosition(1, marker.gameObject.transform.position - offset);

        StartCoroutine(SpawnEffect());
    }

    public void EnableArrow()
    {
        IsStarted = true;
        StartCoroutine(FollowTargetForDuration());
    }

    private IEnumerator FollowTargetForDuration()
    {
        var elapsedTime = 0f;
        var targetScale = new Vector3(1, 1, 1);
        var startPosition = transform.position;
        var originalScale = rectTransform.localScale;
        var goalPosition = marker.gameObject.transform.position;
        while (elapsedTime < duration)
        {
            yield return null;
            if (elapsedTime < 0.17f)
            {
                startPosition += (SetScale(Vector3.Lerp(originalScale, targetScale, Mathf.Min(0.15f, elapsedTime) / 0.15f))) / 2;
            }
            transform.position = Vector3.Lerp(startPosition, goalPosition, elapsedTime / duration);
            lineRenderer.SetPosition(0, transform.position);
            elapsedTime += Time.deltaTime;
        }
        marker.OnRelease();
    }

    private IEnumerator SpawnEffect() // 첫 소환시 이미지가 서서히 보일 수 있도록
    {
        var material = lineRenderer.material;
        Color color = material.color;
        color.a = 0f;
        material.color = color;

        float elapsedTime = 0f;
        var targetScale = new Vector3(.4f, .4f, 1);
        var originalScale = rectTransform.localScale;
        while (elapsedTime < 0.15f)
        {
            yield return null;
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Clamp01(elapsedTime / 0.15f);
            material.color = color;
            SetScale(Vector3.Lerp(originalScale, targetScale, elapsedTime / 0.15f));
        }
        color.a = 1;
        material.color = color;
        SetScale(targetScale);
    }

    private Vector3 SetScale(Vector3 newScale)
    {
        var diffVector = (newScale - transform.localScale) * rectTransform.sizeDelta;
        var diffValue = diffVector.y / 2;
        transform.localScale = newScale;
        Vector3 offset = transform.rotation.eulerAngles.z switch
        {
            270 => new(-diffValue, 0, 0), // <
            90 => new(diffValue, 0, 0), // >
            180 => new(0, diffValue, 0), // ∧
            _ => new(0, -diffValue, 0), // V
        };
        transform.position += offset;
        lineRenderer.SetPosition(0, transform.position);
        return offset;
    }

    private void OnDestroy(){
        StopAllCoroutines();
        marker.ArrowGuide.SetActive(false);
    }
}
