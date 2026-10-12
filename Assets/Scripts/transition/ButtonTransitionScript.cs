using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class ButtonTransitionScript : MonoBehaviour
{
    public Animator transition;
    public float transitionTime = 1f; //mettiamo di default 1 secondo di pausa
    public float delayBeforeTransition = 2f; // Tempo di ritardo prima della transizione

    private bool buttonClicked = false;

    public void LoadScene(string sceneName)
    {
        buttonClicked = true;
        StartCoroutine(DelayedLoadScene(sceneName));
    }

    IEnumerator DelayedLoadScene(string sceneName)
    {
        // Attendere il ritardo prima di iniziare la transizione
        yield return new WaitForSeconds(delayBeforeTransition);

        // Avviare l'animazione di transizione
        transition.SetTrigger("Start");

        // Attendere il tempo di transizione
        yield return new WaitForSeconds(transitionTime);

        // Caricare la scena
        SceneManager.LoadScene(sceneName);
    }
}





/*
public class ButtonTransitionScript : MonoBehaviour
{

    public Animator transition;

    public float transitionTime = 1f; //mettiamo di default 1 secondo di pausa
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator LoadLevel(int levelIndex)
    {
        //Play animation
        transition.SetTrigger("Start");

        //wait
        yield return new WaitForSeconds(transitionTime);

        //Load scene
        SceneManager.LoadScene(levelIndex);
    }
}

*/