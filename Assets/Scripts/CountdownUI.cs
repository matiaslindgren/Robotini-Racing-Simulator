﻿using UnityEngine;
using TMPro;

public class CountdownUI : MonoBehaviour
{
    // Use this for initialization
    void Start()
    {
        EventBus.Subscribe<ProceedToNextPhase>(this, s => {
            // TODO: this doesn't work for playback!
            gameObject.GetComponent<TextMeshProUGUI>().text = "";
        });
        EventBus.Subscribe<SecondsRemaining>(this, s => {
            var txt = s.secondsRemaining > 0 ? "" + s.secondsRemaining : "";
            gameObject.GetComponent<TextMeshProUGUI>().text = txt;
        });
    }
}
