using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenCloseUI : MonoBehaviour
{
    public GameObject UIHolder; //The Holder of your Pause UI (Parent Object)

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) {
            UIHolder.SetActive(!UIHolder.activeSelf); // Turns on and off UI (Pause UI)

            if (UIHolder.activeSelf) { 
                Time.timeScale = 0.0f; //Pauses the game
            }
            else
            {
                Time.timeScale = 1.0f; //Un Pauses the gamed
            }
        }
    }
}
