using System.Collections.Generic;
using UnityEngine;

public class MarkerAnimator : MonoBehaviour
{
    public float StartTime;
    public List<Sprite> SpriteList = new();

    private const float TimePerFrame = 1 / 30f; // TODO: configuration
    private SpriteRenderer spriteRenderer;
    
    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        var count = SpriteList.Count;
        if (count < 1)
        {
            Destroy(gameObject);
            return;
        }

        var elapsedTime = Time.time - StartTime;
        var frame = Mathf.FloorToInt(elapsedTime / TimePerFrame);
        if (frame < 0)
        {
            spriteRenderer.sprite = null;
            return;
        }
        else if (frame >= count)
        {
            return;
        }
        spriteRenderer.sprite = SpriteList[Mathf.Min(frame, count)];
    }
}
