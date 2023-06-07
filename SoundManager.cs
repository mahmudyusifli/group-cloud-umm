using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class SoundManager : AutoSingleton<SoundManager>
{
    [SerializeField] private AudioSource GameEndSource;

    public void PlayGameEnd()
    {
        GameEndSource.Play();
    }
}
