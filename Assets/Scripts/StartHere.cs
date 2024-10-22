using System.Collections;
using UnityEngine;

public class StartHere : MonoBehaviour
{
    public double DestroyTime = -1;

    private SpriteRenderer spriteRenderer;
    private const float FadeDuration = 0.25f; // 페이드 시간 (초 단위)

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (DestroyTime <= 0 || Time.timeAsDouble < DestroyTime) return;

        DestroyTime = -1;
        StartCoroutine(FadeOutSprite());
    }

    private IEnumerator FadeOutSprite()
    {
        var elapsedTime = 0f;
        var color = spriteRenderer.color;
        while (elapsedTime < FadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsedTime / FadeDuration);
            spriteRenderer.color = color;
            yield return null;
        }
        Destroy(gameObject);
    }
}
