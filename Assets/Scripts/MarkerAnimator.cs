using System;
using System.Collections.Generic;
using UnityEngine;

public class MarkerAnimator : MonoBehaviour
{
    public bool Loop = false;
    public double StartTime;
    public MarkerAnimation Animation = new MarkerAnimation(new());

    private SpriteRenderer spriteRenderer;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        var count = Animation.SpriteList.Count;
        if (count < 1)
        {
            return;
        }

        var elapsedTime = Time.timeAsDouble - StartTime;
        if (elapsedTime < 0)
        {
            spriteRenderer.sprite = null;
            return;
        }

        var frame = (int) Math.Floor(elapsedTime * Animation.SampleRate);
        if (Loop)
        {
            frame %= count;
        }
        else if (frame >= count)
        {
            spriteRenderer.sprite = null;
            return;
        }
        spriteRenderer.sprite = Animation.SpriteList[frame];
    }
}
