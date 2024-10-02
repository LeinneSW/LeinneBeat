using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.Experimental.Rendering.Universal.PixelPerfectCamera;

public class MarkerAnimator : MonoBehaviour
{
    public bool Loop = false;
    public double StartTime;
    public List<Sprite> SpriteList = new();

    private int beforeFrame = -1;
    private double sampleRate = 30.0; // TODO: Modifiable by configuration.
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
        if (elapsedTime < 0)
        {
            spriteRenderer.sprite = null;
            return;
        }

        var frame = (int)Math.Floor(elapsedTime * sampleRate);
        if (Loop)
        {
            frame %= count;
        }
        else if (frame >= count)
        {
            spriteRenderer.sprite = null;
            return;
        }
        /*if (Loop && frame != beforeFrame)
        {
            beforeFrame = frame;
            Debug.Log($"now: {frame}");
        }*/
        spriteRenderer.sprite = SpriteList[frame];
    }
}
