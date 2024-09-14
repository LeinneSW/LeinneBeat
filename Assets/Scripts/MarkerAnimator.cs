using System.Collections.Generic;
using UnityEngine;

public class MarkerAnimator : MonoBehaviour
{
    public float StartTime;
    public List<Sprite> SpriteList = new();

    private const float TimePerFrame = 1 / 30f;
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
        if (frame < 0 || frame >= count)
        {
            return;
        }
        spriteRenderer.sprite = SpriteList[frame];
    }
}
