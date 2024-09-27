using System.Collections.Generic;
using UnityEngine;

public class MarkerAnimator : MonoBehaviour
{
    public double StartTime;
    public List<Sprite> SpriteList = new();

    private int SampleRate = 30f; // TODO: Modifiable by configuration.
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

        var elapsedTime = Time.timeAsDouble - StartTime;
        var frame = Mathf.FloorToInt(elapsedTime * SampleRate);
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
