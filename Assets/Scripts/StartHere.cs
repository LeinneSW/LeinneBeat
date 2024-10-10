using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartHere : MonoBehaviour
{
    public double DestroyTime = -1;

    private void Update()
    {
        if (DestroyTime > 0 && Time.timeAsDouble >= DestroyTime)
        {
            // TODO: 서서히 사라지도록 변경
            Destroy(gameObject);
        }
    }
}
