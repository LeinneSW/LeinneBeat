using UnityEngine;
using System.Collections;

public class ComboShutter : MonoBehaviour
{
    private bool up;
    private const float Frequency = 3f;  // 주기 (초당 오르내림 횟수)
    private const float Amplitude = 12f; // 오르내리는 높이의 반경

    private void Start()
    {
        up = gameObject.name.Contains("Up");
        StartCoroutine(StartShutterAnimation());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    public IEnumerator StartShutterAnimation()
    {
        // 시작 전까진 셔터는 움직이지 않는다
        while (!GameManager.Instance.IsStarted)
        {
            yield return null;
        }

        var music = GameManager.Instance.CurrentMusic;
        var remainTime = music.Clip.length + Mathf.Max(0, music.Offset);
        while (remainTime > 0)
        {
            remainTime -= Time.deltaTime;
            var position = transform.position;
            var percent = GameManager.Instance.ShutterPoint * (800 + Amplitude) / 1024;
            var amplitude = Mathf.Sin(Time.time * Frequency * 2 * Mathf.PI) * Amplitude;
            position.y = up ? percent + amplitude - 130 + Amplitude : -(percent + amplitude + 830 + Amplitude);
            transform.position = position;
            yield return null;
        }

        var moveTime = 0f;
        var startPos = transform.position;
        var endPos = transform.position;
        endPos.y = up ? -130 : -830;

        // 현재 높이에 비례한 이동 시간 계산
        const float baseTime = 0.30f;
        var maxY = up ? 670 + Amplitude : -1630 - Amplitude;
        var adjustedTime = baseTime * (startPos.y / maxY); // 높이에 비례한 이동 시간
        while (moveTime < adjustedTime)
        {
            moveTime += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, moveTime / adjustedTime);
            yield return null;
        }
    }
}
