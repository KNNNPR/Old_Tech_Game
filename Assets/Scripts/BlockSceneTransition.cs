using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Hides a story-scene load, then restores the picture in large squares from top to bottom.</summary>
public sealed class BlockSceneTransition : MonoBehaviour
{
    private static BlockSceneTransition active;
    public static bool IsRunning=>active;
    public float Coverage {get;private set;}
    private Image[] blocks;
    private int columns,rows;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()=>active=null;
    public static void Load(string scene)
    {
        if(active)return;
        if(!Application.CanStreamedLevelBeLoaded(scene)){Debug.LogError("Missing transition scene: "+scene);return;}
        var root=new GameObject("Story block transition",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(BlockSceneTransition));
        DontDestroyOnLoad(root);active=root.GetComponent<BlockSceneTransition>();active.StartCoroutine(active.ChangeScene(scene));
    }
    private IEnumerator ChangeScene(string scene)
    {
        var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32767;
        var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        columns=20;rows=Mathf.Max(1,Mathf.CeilToInt(columns*Screen.height/(float)Mathf.Max(1,Screen.width)));blocks=new Image[columns*rows];
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {
            var rect=new GameObject("Black square",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();rect.SetParent(transform,false);
            rect.anchorMin=new Vector2(x/(float)columns,1-(y+1)/(float)rows);rect.anchorMax=new Vector2((x+1)/(float)columns,1-y/(float)rows);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=rect.GetComponent<Image>();image.color=Color.clear;image.raycastTarget=true;blocks[y*columns+x]=image;
        }
        var player=FindFirstObjectByType<FirstPersonController>();if(player)player.SetControlEnabled(false);
        var interactor=FindFirstObjectByType<PlayerInteractor>();if(interactor)interactor.InputEnabled=false;
        var sources=new Dictionary<AudioSource,float>();foreach(var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))sources[source]=source.volume;
        for(float elapsed=0;elapsed<1.25f;elapsed+=Time.unscaledDeltaTime)
        {
            float t=elapsed/1.25f;SetBlocks(t,false);foreach(var sound in sources)if(sound.Key)sound.Key.volume=sound.Value*(1-Mathf.SmoothStep(0,1,t));yield return null;
        }
        SetBlocks(1,false);yield return null;
        yield return SceneManager.LoadSceneAsync(scene);
        yield return new WaitForSecondsRealtime(.15f);
        for(float elapsed=0;elapsed<1.6f;elapsed+=Time.unscaledDeltaTime){SetBlocks(elapsed/1.6f,true);yield return null;}
        SetBlocks(1,true);Destroy(gameObject);
    }
    private void SetBlocks(float progress,bool reveal)
    {
        Coverage=reveal?1-progress:progress;
        for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
        {
            float delay=y/(float)rows*.8f+Mathf.Repeat(x*.618f+y*.37f,1)*.075f;
            float alpha=Mathf.SmoothStep(0,1,(progress-delay)/.12f);
            blocks[y*columns+x].color=new Color(0,0,0,reveal?1-alpha:alpha);
        }
    }
    private void OnDestroy(){if(active==this)active=null;}
}
