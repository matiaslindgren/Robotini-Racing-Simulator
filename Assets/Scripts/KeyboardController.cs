﻿using UnityEngine;
using UniRx;

public class KeyboardController : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            EventBus.Publish(new MotorsToggle());
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            EventBus.Publish(new ResetTimers());
        }
    }
}