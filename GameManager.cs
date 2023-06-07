using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class GameManager : AutoSingleton<GameManager>
{
    [SerializeField] private Button SinglePlayer;
    [SerializeField] private Button MultiPlayer;
    [SerializeField] private GameObject GameMenu;

    public GameMode GameMode { get; set; }
    private void Start()
    {
        SinglePlayer.onClick.AddListener(OnClickSinglePlayer);
        MultiPlayer.onClick.AddListener(OnClickMultiPlayer);
    }

    private void OnClickSinglePlayer()
    {
        GameMenu.SetActive(false);
        GameMode = GameMode.SinglePlayer;
        BoardManager.Instance.RestartGame();
    }  
    
    private void OnClickMultiPlayer()
    {
        GameMenu.SetActive(false);
        GameMode = GameMode.MultiPlayer;
        BoardManager.Instance.RestartGame();
    }

    public void OpenMenu()
    {
        GameMenu.SetActive(true);
    }
}

public enum GameMode
{
    SinglePlayer,
    MultiPlayer,
}
