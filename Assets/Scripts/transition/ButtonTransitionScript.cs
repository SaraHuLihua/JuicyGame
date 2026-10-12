using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class ButtonTransitionScript : MonoBehaviour
{
    public Animator transition;
    public float transitionTime = 1f; //mettiamo di default 1 secondo di pausa
    public float delayBeforeTransition = 2f; // Tempo di ritardo prima della transizione

    [Header("Zoom in prima della transizione")]
    [Tooltip("Animator con l'animazione mainMenuZoomIn (l'oggetto Image del menu). Vuoto = niente zoom.")]
    public Animator zoomAnimator;
    [Tooltip("Nome dello stato dello zoom nel controller (Art/UI/Image.controller).")]
    public string zoomState = "mainMenuZoomIn";
    [Tooltip("Secondi iniziali dell'animazione da saltare: nella clip lo zoom vero parte a circa 0.9 s.")]
    public float zoomSkipStart = 0.9f;
    [Tooltip("Secondi di zoom prima che parta la transizione (lo zoom continua sotto la transizione).")]
    public float zoomBeforeTransition = 0.6f;

    private bool buttonClicked = false;

    public void LoadScene(string sceneName)
    {
        if (buttonClicked) return; // un solo clic
        buttonClicked = true;
        StartCoroutine(DelayedLoadScene(sceneName));
    }

    IEnumerator DelayedLoadScene(string sceneName)
    {
        // 1. Zoom in della camera/immagine appena si clicca
        if (zoomAnimator != null)
        {
            float length = ClipLength(zoomAnimator, zoomState);
            float start = length > 0f ? Mathf.Clamp01(zoomSkipStart / length) : 0f;
            zoomAnimator.Play(zoomState, 0, start);
            yield return new WaitForSeconds(zoomBeforeTransition);
        }

        // Attendere il ritardo prima di iniziare la transizione
        if (delayBeforeTransition > 0f) yield return new WaitForSeconds(delayBeforeTransition);

        // 2. Avviare l'animazione di transizione
        transition.SetTrigger("Start");

        // Attendere il tempo di transizione
        yield return new WaitForSeconds(transitionTime);

        // 3. Caricare la scena
        SceneManager.LoadScene(sceneName);
    }

    static float ClipLength(Animator a, string clipName)
    {
        if (a.runtimeAnimatorController == null) return 0f;
        foreach (AnimationClip c in a.runtimeAnimatorController.animationClips)
            if (c != null && c.name == clipName) return c.length;
        return 0f;
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