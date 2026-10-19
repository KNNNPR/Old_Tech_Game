using UnityEngine;

/// <summary>Menu-only music and UI sounds, controlled by GameSettings through AudioListener.volume.</summary>
public sealed class MenuAudio:MonoBehaviour
{
    [SerializeField] private AudioSource music,interfaceSound,cameraSound,hoverSound,powerSound;
    [SerializeField] private AudioClip mainTheme,press,back,cameraLookup,hover,lever,logoHover,turnScreenOn;
    private float nextHoverTime;
    public float PressDuration=>press?press.length:0;
    private void OnEnable()
    {
        GameSettings.Load();
        Configure(music);Configure(interfaceSound);Configure(cameraSound);Configure(hoverSound);Configure(powerSound);
        if(music&&mainTheme){music.clip=mainTheme;music.loop=true;if(!music.isPlaying)music.Play();}
    }
    private static void Configure(AudioSource source)
    {
        if(!source)return;
        source.ignoreListenerVolume=false;source.spatialBlend=0;source.dopplerLevel=0;source.playOnAwake=false;
    }
    private void PlayInterface(AudioClip clip,float volumeScale=1f)
    {
        if(!interfaceSound||!clip)return;
        if(hoverSound)hoverSound.Stop();
        interfaceSound.Stop();interfaceSound.clip=clip;interfaceSound.PlayOneShot(clip,volumeScale);
    }
    public void PlayPress()=>PlayInterface(press);
    public void PlayBack()=>PlayInterface(back);
    public void PlayLever()=>PlayInterface(lever,.5f);
    public void PlayScreenPower()
    {
        if(!powerSound||!turnScreenOn)return;
        powerSound.Stop();powerSound.clip=turnScreenOn;powerSound.loop=false;powerSound.Play();
        powerSound.SetScheduledEndTime(AudioSettings.dspTime+Mathf.Min(1f,turnScreenOn.length));
    }
    public void PlayHover()=>PlayHover(hover);
    public void PlayLogoHover()=>PlayHover(logoHover);
    private void PlayHover(AudioClip clip)
    {
        if(!hoverSound||!clip||Time.unscaledTime<nextHoverTime)return;
        nextHoverTime=Time.unscaledTime+.07f;
        hoverSound.clip=clip;hoverSound.Play();
    }
    public void PlayCameraApproach()
    {
        if(!cameraSound||!cameraLookup)return;
        cameraSound.clip=cameraLookup;cameraSound.Play();
    }
    public void StopCameraApproach(){if(cameraSound)cameraSound.Stop();}
    private void OnDisable(){if(music)music.Stop();if(interfaceSound)interfaceSound.Stop();if(hoverSound)hoverSound.Stop();if(powerSound)powerSound.Stop();StopCameraApproach();}
}
