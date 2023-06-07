using System;
using UnityEngine;

public class Token : MonoBehaviour
{
    [SerializeField] private Animator Animator;

    private bool _falling;
    private Vector2 _fallPos;
    private float _fallSpeed;
    private void Update()
    {
        if(!_falling)
            return;
        
        _fallSpeed += Time.deltaTime;
        transform.position = new Vector2(transform.position.x, transform.position.y - _fallSpeed);
        
        if (_fallPos.y >= transform.position.y)
        {
            _falling = false;
            transform.position = _fallPos;
            Animator.Play("Jump");
        }
    }

    public void Drop(Vector2 targetPos)
    {
        _falling = true;
        _fallPos = targetPos;
        transform.position = new Vector2(targetPos.x, transform.position.y);
    }
}