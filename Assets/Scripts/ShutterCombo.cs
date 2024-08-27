using UnityEngine;
using System.Collections;

public class ShutterCombo : MonoBehaviour
{
    private bool up;
    private readonly float amplitude = 8f;  // 오르내리는 높이의 반경
    private readonly float frequency = 3.5f;  // 주기 (초당 오르내림 횟수)

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
        if (!GameManager.Instance.BackgroundMusic.isPlaying)
        {
            // 시작 전까진 셔터는 움직이지 않는다
            yield return null;
        }

        while (GameManager.Instance.BackgroundMusic.isPlaying)
        {
            var position = transform.position;
            var percent = GameManager.Instance.ShutterPoint * (800 + amplitude) / 1024;
            var animation = Mathf.Sin(Time.time * frequency * 2 * Mathf.PI) * amplitude;
            position.y = up ? (percent + animation - 130 + amplitude) : -(percent + animation + 830 + amplitude);
            transform.position = position;
            yield return null;
        } 

        // TODO: 곡이 종료되면 셔터가 닫힘
    }
}
