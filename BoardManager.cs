using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Utilities;
using Random = UnityEngine.Random;

public class BoardManager : AutoSingleton<BoardManager>
{
    [SerializeField] private Button ResetButton;
    [SerializeField] private Button MenuButton;
    [SerializeField] private TextMeshProUGUI AnnouncementText;
    [SerializeField] private TextMeshProUGUI PlayerTurnText;
    
    [SerializeField] private Token RedTokenPrefab;
    [SerializeField] private Token YellowTokenPrefab;
    [SerializeField] private GameObject HighlightPrefab;

    [SerializeField] private Vector2[] TokenSpawnPositions;
    [SerializeField] private float FirstTokenRowY;
    [SerializeField] private float TokenRowHeight;
    
    public List<List<int>> BoardList;
    public int TurnCount { get; set; }
    private bool WaitingMove { get; set; }
    private Token TokenHolded { get; set; }

    private HashSet<Vector2Int> WinnerCoordinates = new();

    private List<Token> _tokensOnBoard = new();

    private void Start()
    {
        ResetButton.onClick.AddListener(OnRestartButton);
        MenuButton.onClick.AddListener(OnMenuButton);
    }

    public void RestartGame()
    {
        BoardList = new();
        for (int i = 0; i < 7; i++)
            BoardList.Add(new());

        foreach (var token in _tokensOnBoard)
            Destroy(token.gameObject);
        _tokensOnBoard.Clear();
        
        TokenHolded = null;
        TurnCount = 0;
        WinnerCoordinates.Clear();
        WaitingMove = true;
        SetPlayerTurnText();
    }

    private void OnRestartButton()
    {
        if (!WaitingMove)
            return;
        RestartGame();
    }

    private void OnMenuButton()
    {
        if (!WaitingMove) 
            return;
        
        WaitingMove = false;
        GameManager.Instance.OpenMenu();
    }

    private void Update()
    {
        if(!WaitingMove)
            return;

        if(IsPointerOverUIElement() && TokenHolded == null)
            return;

        var mousePos = CameraManager.Instance.Camera.ScreenToWorldPoint(Input.mousePosition);
        if (Input.GetMouseButtonDown(0))
        {
            HoldToken();
            MoveToken(mousePos);
        }
        else if(Input.GetMouseButtonUp(0))
        {
            DropToken(mousePos);
        }
        else if(Input.GetMouseButton(0))
        {
            MoveToken(mousePos);
        }
        else
        {
            CancelTokenHold();
        }
    }
    
    private bool IsPointerOverUIElement()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        List<RaycastResult> raysastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raysastResults);
        return raysastResults.Count > 0;
    }

    private void HoldToken()
    {
        TokenHolded = Instantiate(GetCurrentPlayersToken());
        _tokensOnBoard.Add(TokenHolded);
    }

    private void DropToken(Vector2 input)
    {
        if(TokenHolded == null)
            return;
        
        int column = GetClosestTokenSpawnPositionIndex(input);
        TokenHolded.Drop(GetPositionFromCoordinates(new Vector2Int(column, BoardList[column].Count)));
        TokenHolded = null;
        
        BoardList[column].Add((int)GetCurrentPlayer());
        EndTurn();
    }

    private void EndTurn()
    {
        CheckWinner();
        
        PlayerTurnText.text = "";
        
        if (WinnerCoordinates.Count != 0)
        {
            // game ended
            var enumerator = WinnerCoordinates.GetEnumerator();
            enumerator.MoveNext();
            var winnerPos = enumerator.Current;
            Player winner = (Player)BoardList[winnerPos.x][winnerPos.y];
            StartCoroutine(PlayerWon(winner));
            return;
        }

        if (TurnCount == 6 * 7 - 1)
        {
            //tie!
            StartCoroutine(Tie());
            return;
        }
        
        TurnCount++;
        SetPlayerTurnText();

        if (GameManager.Instance.GameMode == GameMode.SinglePlayer && GetCurrentPlayer() == Player.Red)
            StartCoroutine(PlayComputerTurn());
    }

    private IEnumerator PlayComputerTurn()
    {
        WaitingMove = false;
        int randomMoveFake = Random.Range(0, 7);
        int randomMove = Random.Range(0, 7);
        Vector2 tokenPosFake = TokenSpawnPositions[randomMoveFake];
        Vector2 tokenPos = TokenSpawnPositions[randomMove];
        HoldToken();
        MoveToken(tokenPosFake);
        yield return new WaitForSeconds(Random.Range(.25f, 1.25f));
        MoveToken(tokenPos);
        yield return new WaitForSeconds(Random.Range(1f, 2f));
        DropToken(tokenPos);
        WaitingMove = true;
    }

    private void SetPlayerTurnText()
    {
        var playerName = GetCurrentPlayer().ToString();
        PlayerTurnText.text = $"<color={playerName.ToLowerInvariant()}>{playerName}</color> Players Turn...";
    }

    private IEnumerator PlayerWon(Player player)
    {
        WaitingMove = false;
        AnnouncementText.text = $"Player {player} Wins!";
        SoundManager.Instance.PlayGameEnd();
        StartCoroutine(HighlightWinnerMoves());
        yield return new WaitForSeconds(4);
        AnnouncementText.text = "";
        RestartGame();
    }
    
    private IEnumerator Tie()
    {
        WaitingMove = false;
        AnnouncementText.text = $"Tie!";
        SoundManager.Instance.PlayGameEnd();
        yield return new WaitForSeconds(4);
        AnnouncementText.text = "";
        RestartGame();
    }

    private IEnumerator HighlightWinnerMoves()
    {
        List<GameObject> highlights = new();
        foreach (var winnerCord in WinnerCoordinates)
        {
            var pos = GetPositionFromCoordinates(winnerCord);
            var hl = Instantiate(HighlightPrefab, pos, Quaternion.identity);
            hl.transform.rotation = Quaternion.Euler(new Vector3(0, 0, Random.Range(0, 360)));
            highlights.Add(hl);
            yield return new WaitForSeconds(1.5f / WinnerCoordinates.Count);
        }
        yield return new WaitForSeconds(2.5f);

        foreach (var hl in highlights)
            Destroy(hl);
    }

    private void CheckWinner()
    {
        CheckWinnerVertical();
        CheckWinnerHorizontal();
        CheckWinnerDiagonalDownLeft();
        CheckWinnerDiagonalDownRight();
        CheckWinnerDiagonalUpLeft();
        CheckWinnerDiagonalUpRight();
    }

    private void CheckWinnerDiagonalDownLeft()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        Vector2Int dir = new Vector2Int(-1, -1);
        
        for (int c = 0; c < 7; c++)
        {
            var collumn = BoardList[c];
            
            for (int r = 0; r < collumn.Count; r++)
            {
                var controlCord = new Vector2Int(c, r);

                int player = BoardList[controlCord.x][controlCord.y];
                
                for (int i = 0; i < 4; i++)
                {
                    var cord = controlCord + dir * i;

                    if(cord.y is < 0 or > 5 || cord.x is < 0 or > 6 )
                        break;

                    if(cord.y >= BoardList[cord.x].Count)
                        break;
                    
                    if(BoardList[cord.x][cord.y] != player)
                        break;

                    if (i == 3)
                    {
                        foundWinner = true;
                        winnerPos = controlCord;
                    }
                }
            }
            
            if(foundWinner)
                break;
        }

        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(winnerPos + dir * i);
        }
    }  
    
    private void CheckWinnerDiagonalDownRight()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        Vector2Int dir = new Vector2Int(1, -1);
        
        for (int c = 0; c < 7; c++)
        {
            var collumn = BoardList[c];
            
            for (int r = 0; r < collumn.Count; r++)
            {
                var controlCord = new Vector2Int(c, r);

                int player = BoardList[controlCord.x][controlCord.y];
                
                for (int i = 0; i < 4; i++)
                {
                    var cord = controlCord + dir * i;

                    if(cord.y is < 0 or > 5 || cord.x is < 0 or > 6 )
                        break;
                    
                    if(cord.y >= BoardList[cord.x].Count)
                        break;
                    
                    if(BoardList[cord.x][cord.y] != player)
                        break;

                    if (i == 3)
                    {
                        foundWinner = true;
                        winnerPos = controlCord;
                    }
                }
            }
            
            if(foundWinner)
                break;
        }

        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(winnerPos + dir * i);
        }
    }
    
    private void CheckWinnerDiagonalUpRight()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        Vector2Int dir = new Vector2Int(1, 1);
        
        for (int c = 0; c < 7; c++)
        {
            var collumn = BoardList[c];
            
            for (int r = 0; r < collumn.Count; r++)
            {
                var controlCord = new Vector2Int(c, r);

                int player = BoardList[controlCord.x][controlCord.y];
                
                for (int i = 0; i < 4; i++)
                {
                    var cord = controlCord + dir * i;

                    if(cord.y is < 0 or > 5 || cord.x is < 0 or > 6 )
                        break;
                    
                    if(cord.y >= BoardList[cord.x].Count)
                        break;
                    
                    if(BoardList[cord.x][cord.y] != player)
                        break;

                    if (i == 3)
                    {
                        foundWinner = true;
                        winnerPos = controlCord;
                    }
                }
            }
            
            if(foundWinner)
                break;
        }

        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(winnerPos + dir * i);
        }
    }
    
    private void CheckWinnerDiagonalUpLeft()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        Vector2Int dir = new Vector2Int(-1, 1);
        
        for (int c = 0; c < 7; c++)
        {
            var collumn = BoardList[c];
            
            for (int r = 0; r < collumn.Count; r++)
            {
                var controlCord = new Vector2Int(c, r);

                int player = BoardList[controlCord.x][controlCord.y];
                
                for (int i = 0; i < 4; i++)
                {
                    var cord = controlCord + dir * i;

                    if(cord.y is < 0 or > 5 || cord.x is < 0 or > 6 )
                        break;
                    
                    if(cord.y >= BoardList[cord.x].Count)
                        break;
                    
                    if(BoardList[cord.x][cord.y] != player)
                        break;

                    if (i == 3)
                    {
                        foundWinner = true;
                        winnerPos = controlCord;
                    }
                }
            }
            
            if(foundWinner)
                break;
        }

        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(winnerPos + dir * i);
        }
    }
    
    private void CheckWinnerVertical()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        
        for (var c = 0; c < 7; c++)
        {
            var collumn = BoardList[c];
            if (collumn.Count < 4)
                continue;
            
            var currentStreakCount = 0;
            var currentStreakPlayer = -1;

            for (var r = 0; r < collumn.Count; r++)
            {
                var token = collumn[r];
                if (token == currentStreakPlayer)
                {
                    currentStreakCount++;

                    if (currentStreakCount == 4)
                    {
                        foundWinner = true;
                        winnerPos = new Vector2Int(c, r);
                        break;
                    }
                }
                else
                {
                    currentStreakCount = 1;
                    currentStreakPlayer = token;
                }
            }
            
            if(foundWinner)
                break;
        }
        
        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(new Vector2Int(winnerPos.x, winnerPos.y - i));
        }
    }
    
    private void CheckWinnerHorizontal()
    {
        bool foundWinner = false;
        Vector2Int winnerPos = Vector2Int.zero;
        
        for (var r = 0; r < 6; r++)
        {
            var currentStreakCount = 0;
            var currentStreakPlayer = -1;
            
            for (var c = 0; c < 7; c++)
            {
                if(BoardList[c].Count <= r)
                {
                    currentStreakCount = 0;
                    currentStreakPlayer = -1;
                    continue;
                }
                
                if (BoardList[c][r] == currentStreakPlayer)
                {
                    currentStreakCount++;

                    if (currentStreakCount == 4)
                    {
                        foundWinner = true;
                        winnerPos = new Vector2Int(c, r);
                        break;
                    }
                }
                else
                {
                    currentStreakCount = 1;
                    currentStreakPlayer = BoardList[c][r];
                }
            }
            
            if(foundWinner)
                break;
        }

        if (foundWinner)
        {
            for (int i = 0; i < 4; i++)
                WinnerCoordinates.Add(new Vector2Int(winnerPos.x - i, winnerPos.y));
        }
    }

    private void MoveToken(Vector2 input)
    {
        if(TokenHolded == null)
            return;
        
        var tokenPos = TokenSpawnPositions[GetClosestTokenSpawnPositionIndex(input)];
        TokenHolded.transform.position = tokenPos;
    }

    private void CancelTokenHold()
    {
        if(TokenHolded == null)
            return;
        
        _tokensOnBoard.Remove(TokenHolded);
        Destroy(TokenHolded);
        TokenHolded = null;
    }

    private Vector2 GetPositionFromCoordinates(Vector2Int coordinates)
    {
        return new Vector2(TokenSpawnPositions[coordinates.x].x, FirstTokenRowY + coordinates.y * TokenRowHeight);
    }

    private int GetClosestTokenSpawnPositionIndex(Vector2 input)
    {
        int closestPosIndex = 0;
        float closestDistance = Mathf.Infinity;
        for (int i = 0; i < TokenSpawnPositions.Length; i++)
        {
            if(BoardList[i].Count == 6)
                continue;
            
            var distance = Vector2.Distance(TokenSpawnPositions[i], input);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPosIndex = i;
            }
        }

        return closestPosIndex;
    }

    private Token GetCurrentPlayersToken()
    {
        if (TurnCount % 2 == 1)
            return RedTokenPrefab;
        return YellowTokenPrefab;
    }

    private Player GetCurrentPlayer()
    {
        return (Player)(TurnCount % 2 + 1);
    }
}

public enum Player
{
    Yellow = 1,
    Red = 2,
}