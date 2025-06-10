using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ball : MonoBehaviour
{
    private bool isCueBall = false;
    public bool IsCueBall
    {
        get { return isCueBall; }
        set { isCueBall = value; }
    }
    private bool isBlackBall = false;
    public bool IsBlackBall
    {
        get { return isBlackBall; }
        set { isBlackBall = value; }
    }
    private int ballNumber = 0; // 0 for cue ball, 8 for black ball, 1-7 & 9-15 for other balls
    public int BallNumber
    {
        get { return ballNumber; }
        set { ballNumber = value; }
    }
    
    // 保留这个方法用于兼容性，但不再自动加载网格
    public void makeBall(int number)
    {
        BallNumber = number;
        IsCueBall = (number == 0);
        IsBlackBall = (number == 8);
        
        // 不再处理网格赋值，由GameInit负责
    }

    // Update is called once per frame
    void Update()
    {
        // 空的Update方法
    }
}
