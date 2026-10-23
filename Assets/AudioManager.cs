using UnityEngine;
using UnityEngine.InputSystem;

public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip[] audios;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }



    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            PlayAudio();
        }
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            PauseAudio();
        }
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            PlaySoundRandom();
        }
    }
    public void PlayAudio()
    {
        print("m");
        audioSource.Play();
    }
    public void PauseAudio()
    {
        print("p");
        audioSource.Pause();
    }
    public void PlaySoundRandom()
    {
        int randomNum = Random.Range(0,audios.Length);
        audioSource.PlayOneShot(audios[randomNum]);
    }
}
