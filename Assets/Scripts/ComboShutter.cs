using UnityEngine;
using System.Collections;

public class ComboShutter : MonoBehaviour
{
    private bool up;
    private readonly float frequency = 3f;  // 주기 (초당 오르내림 횟수)
    private readonly float amplitude = 12f; // 오르내리는 높이의 반경

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
        while (GameManager.Instance.StartTime <= 0)
        {
            yield return null;
        }

        var music = GameManager.Instance.SelectedMusic;
        var remainTime = music.clip.length + Mathf.Max(0, music.StartOffset);
        while (remainTime > 0)
        {
            remainTime -= Time.deltaTime;
            var position = transform.position;
            var percent = GameManager.Instance.ShutterPoint * (800 + amplitude) / 1024;
            var animation = Mathf.Sin(Time.time * frequency * 2 * Mathf.PI) * amplitude;
            position.y = up ? (percent + animation - 130 + amplitude) : -(percent + animation + 830 + amplitude);
            transform.position = position;
            yield return null;
        }

        var moveTime = 0f;
        var startPos = transform.position;
        var endPos = transform.position;
        endPos.y = up ? -130 : -830;
        while (moveTime < .5f)
        {
            moveTime += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, moveTime / .5f);
            yield return null;
        }
    }
}
