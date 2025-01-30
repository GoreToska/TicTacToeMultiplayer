using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ClientLoadingScreen : MonoBehaviour
{
    protected class LoadingProgressBar
    {
        public Slider ProgressBar { get; set; }
        public Text NameText { get; set; }

        public LoadingProgressBar(Slider otherPlayerProgressBar, Text otherPlayerNameText)
        {
            ProgressBar = otherPlayerProgressBar;
            NameText = otherPlayerNameText;
        }

        public void UpdateProgress(float value, float newValue)
        {
            ProgressBar.value = newValue;
        }
    }

    [SerializeField] private GameObject loadingWindow;
    [SerializeField] private GameObject waitingWindow;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float delayBeforeFadeOut = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.1f;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text sceneName;
    [SerializeField] private List<Slider> otherPlayersProgressBars;
    [SerializeField] private List<Text> otherPlayerNamesTexts;

    [SerializeField] protected LoadingProgressManager loadingProgressManager;
    protected Dictionary<ulong, LoadingProgressBar> loadingProgressBars = new Dictionary<ulong, LoadingProgressBar>();

    private bool loadingScreenRunning;
    private Coroutine fadeOutCoroutine;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        //loadingProgressManager.OnLoadingEnded += ShowWaitingWindow;
        loadingProgressManager.OnAllPlayersLoaded += OnAllPlayersLoaded;
    }

    private void Start()
    {
        SetCanvasVisibility(false);
    }

    void Update()
    {
        if (loadingScreenRunning)
        {
            progressBar.value = loadingProgressManager.LocalProgress;
        }
    }

    public void StartLoadingScreen(string sceneName)
    {
        SetCanvasVisibility(true);
        loadingWindow.SetActive(true);
        waitingWindow.SetActive(false);
        loadingScreenRunning = true;
        UpdateLoadingScreen(sceneName);
    }

    public void CompleteLoading()
    {
        loadingWindow.SetActive(false);
        waitingWindow.SetActive(true);
    }

    public void OnAllPlayersLoaded(object sender, EventArgs eventArgs)
    {
        HideWaitingScreen();
    }

    public void HideWaitingScreen()
    {
        if (loadingScreenRunning)
        {
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }

            fadeOutCoroutine = StartCoroutine(FadeOutCoroutine());
        }
    }

    public void UpdateLoadingScreen(string sceneName)
    {
        if (loadingScreenRunning)
        {
            if (fadeOutCoroutine != null)
            {
                StopCoroutine(fadeOutCoroutine);
            }
        }
    }

    private void SetCanvasVisibility(bool visible)
    {
        canvasGroup.alpha = visible ? 1 : 0;
        canvasGroup.blocksRaycasts = visible;
    }

    private IEnumerator FadeOutCoroutine()
    {
        yield return new WaitForSeconds(delayBeforeFadeOut);
        loadingScreenRunning = false;

        float currentTime = 0;
        while (currentTime < fadeOutDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, currentTime / fadeOutDuration);
            yield return null;
            currentTime += Time.deltaTime;
        }

        SetCanvasVisibility(false);
    }
}