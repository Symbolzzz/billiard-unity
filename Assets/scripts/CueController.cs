using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GameState
{
    Aiming,     // 瞄准状态
    Charging,   // 蓄力状态
    Striking,   // 击球动画状态
    Waiting,    // 等待所有球静止状态
    Ready       // 球已静止，等待用户进入瞄准模式
}

public class CueController : MonoBehaviour
{
    [Header("References")]
    public Transform cueBall;           // 母球的Transform (可选，会自动查找)
    public GameObject cueModel;         // 球杆模型
    public LineRenderer aimingLine;     // 瞄准线

    [Header("Control Settings")]
    public float rotationSpeed = 100f;  // 旋转速度
    public float maxPower = 100f;       // 最大力度
    public float powerChargeSpeed = 50f; // 蓄力速度
    public float maxPullBackDistance = 0.5f; // 球杆最大后拉距离
    public KeyCode aimingKey = KeyCode.Space; // 进入/退出瞄准模式的按键

    [Header("Physics Settings")]
    public float forceMultiplier = 10f; // 力度乘数
    public float ballStopThreshold = 0.01f; // 球静止阈值

    [Header("Aiming Line Settings")]
    public int lineSegments = 50;       // 瞄准线段数
    public float lineLength = 5f;       // 瞄准线长度
    public LayerMask ballLayer = 1;     // 球的层级掩码

    [Header("Auto Find Settings")]
    public float cueBallCheckInterval = 1f; // 检查母球的间隔时间
    public bool debugMode = true;       // 调试模式

    // 私有变量
    private GameState gameState = GameState.Ready; // 默认状态改为Ready
    private float currentPower = 0f;
    private Vector3 initialCuePosition;
    private List<Rigidbody> allBalls = new List<Rigidbody>();
    private Rigidbody cueBallRigidbody;
    private float lastCueBallCheck = 0f;
    private bool cueBallFound = false;

    private float strikeTime = 0f;           // 击球时间
    private float minWaitTimeAfterStrike = 0.5f; // 击球后最小等待时间

    void Start()
    {
        // 初始化
        InitializeCueController();
    }

    void InitializeCueController()
    {
        // 记录球杆初始位置
        if (cueModel != null)
        {
            initialCuePosition = cueModel.transform.localPosition;
            // 初始状态下隐藏球杆
            cueModel.SetActive(false);
        }

        // 尝试查找母球
        FindCueBall();

        // 初始化瞄准线
        InitializeAimingLine();

        // 确保初始状态正确
        gameState = GameState.Ready;
    }

    void FindCueBall()
    {
        // 如果已经手动指定了母球，直接使用
        if (cueBall != null)
        {
            cueBallRigidbody = cueBall.GetComponent<Rigidbody>();
            cueBallFound = true;
            if (debugMode) Debug.Log("使用手动指定的母球");
            return;
        }

        // 查找场景中所有带有ball组件的对象
        ball[] ballComponents = FindObjectsOfType<ball>();

        foreach (ball ballComponent in ballComponents)
        {
            // 检查是否是母球
            if (ballComponent.IsCueBall || ballComponent.BallNumber == 0)
            {
                cueBall = ballComponent.transform;
                cueBallRigidbody = ballComponent.GetComponent<Rigidbody>();
                cueBallFound = true;

                if (debugMode) Debug.Log($"找到母球: {ballComponent.name}");
                break;
            }
        }

        if (!cueBallFound)
        {
            if (debugMode) Debug.LogWarning("未找到母球，将在运行时继续查找");
        }

        // 更新所有球的列表
        FindAllBalls();
    }

    void FindAllBalls()
    {
        // 查找场景中所有带有ball组件的对象
        ball[] ballComponents = FindObjectsOfType<ball>();
        allBalls.Clear();

        foreach (ball ballComponent in ballComponents)
        {
            Rigidbody rb = ballComponent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                allBalls.Add(rb);
            }
        }

        if (debugMode) Debug.Log($"找到 {allBalls.Count} 个球用于监控");
    }

    void InitializeAimingLine()
    {
        if (aimingLine == null)
        {
            // 如果没有指定LineRenderer，尝试创建一个
            GameObject lineObject = new GameObject("AimingLine");
            lineObject.transform.SetParent(transform);
            aimingLine = lineObject.AddComponent<LineRenderer>();
        }

        // 配置瞄准线
        aimingLine.material = new Material(Shader.Find("Sprites/Default"));
        aimingLine.startColor = Color.white;
        aimingLine.endColor = Color.white;
        aimingLine.startWidth = 0.02f;
        aimingLine.endWidth = 0.02f;
        aimingLine.positionCount = lineSegments;
        aimingLine.enabled = false;
    }

    void Update()
    {
        // 定期检查母球是否存在（适用于运行时加载的情况）
        if (!cueBallFound || cueBall == null)
        {
            if (Time.time - lastCueBallCheck > cueBallCheckInterval)
            {
                lastCueBallCheck = Time.time;
                FindCueBall();
            }

            // 如果还没有找到母球，不执行其他逻辑
            if (!cueBallFound || cueBall == null)
            {
                if (debugMode && Time.frameCount % 300 == 0) // 每5秒输出一次警告
                {
                    Debug.LogWarning("等待母球加载...");
                }
                return;
            }
        }

        // 处理瞄准键输入
        HandleAimingInput();

        // 只在瞄准和蓄力状态下，CuePivot位置才跟随母球
        if (cueBall != null && (gameState == GameState.Aiming || gameState == GameState.Charging))
        {
            transform.position = cueBall.position;
        }

        // 根据当前状态执行相应逻辑
        switch (gameState)
        {
            case GameState.Ready:
                HandleReady();
                break;
            case GameState.Aiming:
                HandleAiming();
                break;
            case GameState.Charging:
                HandleCharging();
                break;
            case GameState.Striking:
                // 击球动画状态，通常很短暂
                break;
            case GameState.Waiting:
                HandleWaiting();
                break;
        }
    }

    void HandleAimingInput()
    {
        if (Input.GetKeyDown(aimingKey))
        {
            if (gameState == GameState.Ready)
            {
                // 从Ready状态进入瞄准模式
                EnterAimingMode();
            }
            else if (gameState == GameState.Aiming)
            {
                // 从瞄准模式退出到Ready状态（回到全局视角）
                ExitAimingMode();
            }
        }
    }

    void HandleReady()
    {
        // Ready状态：球已静止，等待用户按键进入瞄准模式
        // 隐藏球杆和瞄准线
        if (cueModel != null && cueModel.activeSelf)
        {
            cueModel.SetActive(false);
        }
        
        if (aimingLine != null && aimingLine.enabled)
        {
            aimingLine.enabled = false;
        }
    }

    void EnterAimingMode()
    {
        gameState = GameState.Aiming;
        
        // 显示球杆
        if (cueModel != null)
        {
            cueModel.SetActive(true);
            cueModel.transform.localPosition = initialCuePosition;
        }
        
        // 定位到母球位置
        if (cueBall != null)
        {
            transform.position = cueBall.position;
        }
        
        if (debugMode) Debug.Log("进入瞄准模式 - 按空格键回到全局视角观察");
    }

    void ExitAimingMode()
    {
        gameState = GameState.Ready;
        
        // 隐藏球杆和瞄准线
        if (cueModel != null)
        {
            cueModel.SetActive(false);
        }
        
        if (aimingLine != null)
        {
            aimingLine.enabled = false;
        }
        
        if (debugMode) Debug.Log("退出瞄准模式，回到全局视角观察 - 按空格键重新瞄准");
    }

    // 添加一个新方法来检查是否可以进入蓄力状态
    void HandleAiming()
    {
        // 显示瞄准线
        if (aimingLine != null)
        {
            aimingLine.enabled = true;
            UpdateAimingLine();
        }

        // 鼠标水平移动控制球杆旋转
        float mouseX = -Input.GetAxis("Mouse X");
        if (Mathf.Abs(mouseX) > 0.01f)
        {
            transform.RotateAround(cueBall.position, Vector3.up, mouseX * rotationSpeed * Time.deltaTime);
        }

        // 检测鼠标左键按下，切换到蓄力状态
        if (Input.GetMouseButtonDown(0))
        {
            StartCharging();
        }
    }

    void UpdateAimingLine()
    {
        if (cueBall == null) return;

        Vector3 startPos = cueBall.position;
        Vector3 direction = transform.forward;

        // 计算瞄准线各个点的位置
        for (int i = 0; i < lineSegments; i++)
        {
            float t = (float)i / (lineSegments - 1);
            Vector3 point = startPos + direction * (lineLength * t);

            // 可以在这里添加碰撞检测，让瞄准线在碰到球或边界时停止
            // 简化版本：直接画一条直线
            aimingLine.SetPosition(i, point);
        }
    }

    void StartCharging()
    {
        gameState = GameState.Charging;
        currentPower = 0f;

        // 隐藏瞄准线
        if (aimingLine != null)
        {
            aimingLine.enabled = false;
        }

        if (debugMode) Debug.Log("开始蓄力");
    }

    // 修改蓄力状态，添加取消功能
    void HandleCharging()
    {
        // 如果按下空格键，取消蓄力回到瞄准状态
        if (Input.GetKeyDown(aimingKey))
        {
            CancelCharging();
            return;
        }

        // 蓄力逻辑
        currentPower += powerChargeSpeed * Time.deltaTime;
        currentPower = Mathf.Min(currentPower, maxPower);

        // 根据蓄力值后拉球杆
        float pullBackDistance = (currentPower / maxPower) * maxPullBackDistance;
        Vector3 newPosition = initialCuePosition + new Vector3(0, 0, pullBackDistance);
        if (cueModel != null)
        {
            cueModel.transform.localPosition = newPosition;
        }

        // 检测鼠标左键松开，执行击球
        if (Input.GetMouseButtonUp(0))
        {
            Strike();
        }

        // 显示当前力度（可选）
        if (debugMode && Time.frameCount % 30 == 0) // 每半秒显示一次
        {
            Debug.Log($"当前力度: {currentPower:F1}/{maxPower} - 按空格键取消");
        }
    }

    // 新增取消蓄力的方法
    void CancelCharging()
    {
        gameState = GameState.Aiming;
        currentPower = 0f;
        
        // 重置球杆位置
        if (cueModel != null)
        {
            cueModel.transform.localPosition = initialCuePosition;
        }
        
        if (debugMode) Debug.Log("取消蓄力，回到瞄准状态");
    }

    void Strike()
    {
        gameState = GameState.Striking;

        // 记录击球时间
        strikeTime = Time.time;

        if (debugMode) Debug.Log($"击球! 力度: {currentPower:F1}");

        // 计算击球方向和力度
        Vector3 forceDirection = -transform.forward;
        float forceAmount = currentPower * forceMultiplier;

        // 给母球施加力
        if (cueBallRigidbody != null)
        {
            cueBallRigidbody.AddForce(forceDirection * forceAmount, ForceMode.Impulse);
        }
        else
        {
            Debug.LogError("母球刚体组件丢失!");
        }

        // 隐藏球杆
        if (cueModel != null)
        {
            cueModel.SetActive(false);
        }

        // 切换到等待状态
        gameState = GameState.Waiting;
    }

    void HandleWaiting()
    {
        // 确保击球后至少等待一段时间再检查球的状态
        if (Time.time - strikeTime < minWaitTimeAfterStrike)
        {
            if (debugMode && Time.frameCount % 60 == 0) // 每秒输出一次
            {
                float remainingTime = minWaitTimeAfterStrike - (Time.time - strikeTime);
                Debug.Log($"等待击球生效... 剩余时间: {remainingTime:F1}秒");
            }
            return;
        }

        // 检查所有球是否都静止了
        if (AreAllBallsSleeping())
        {
            if (debugMode) Debug.Log("所有球已静止，进入Ready状态，按空格键开始瞄准");
            PrepareNextShot();
        }
    }

    bool AreAllBallsSleeping()
    {
        // 如果球的列表为空，重新查找
        if (allBalls.Count == 0)
        {
            FindAllBalls();
        }

        foreach (Rigidbody rb in allBalls)
        {
            if (rb == null) continue; // 跳过已销毁的球

            if (rb.velocity.magnitude > ballStopThreshold ||
                rb.angularVelocity.magnitude > ballStopThreshold)
            {
                return false; // 还有球在运动
            }
        }

        // 额外检查：确保母球确实有过运动
        // 如果击球力度很小，母球可能根本没有明显移动
        if (cueBallRigidbody != null)
        {
            // 检查母球是否曾经达到一定速度（表明击球生效了）
            if (Time.time - strikeTime < 1f && cueBallRigidbody.velocity.magnitude < 0.1f)
            {
                // 如果击球后1秒内母球速度仍然很小，可能是击球力度太小
                if (debugMode && Time.frameCount % 60 == 0)
                {
                    Debug.Log("母球移动缓慢，继续等待...");
                }
                return false;
            }
        }

        return true; // 所有球都静止了
    }

    void PrepareNextShot()
    {
        // 切换到Ready状态，等待用户按键进入瞄准模式
        gameState = GameState.Ready;

        // 隐藏球杆
        if (cueModel != null)
        {
            cueModel.SetActive(false);
        }

        // 重置力度
        currentPower = 0f;

        if (debugMode) Debug.Log("准备下一次击球，按空格键进入瞄准模式");
    }

    // 公共方法，供其他脚本调用
    public GameState GetCurrentState()
    {
        return gameState;
    }

    public float GetCurrentPower()
    {
        return currentPower;
    }

    public float GetPowerPercentage()
    {
        return currentPower / maxPower;
    }

    public bool IsCueBallFound()
    {
        return cueBallFound && cueBall != null;
    }

    // 强制重新查找母球（用于球被重新放置后）
    public void RefreshCueBall()
    {
        cueBallFound = false;
        cueBall = null;
        cueBallRigidbody = null;
        FindCueBall();
    }

    // 强制重置到瞄准状态（调试用）
    public void ResetToAiming()
    {
        gameState = GameState.Aiming;
        currentPower = 0f;

        if (cueModel != null)
        {
            cueModel.SetActive(true);
            cueModel.transform.localPosition = initialCuePosition;
        }

        // 重新查找母球
        RefreshCueBall();
    }

    // 强制进入Ready状态
    public void SetToReady()
    {
        gameState = GameState.Ready;
        currentPower = 0f;

        if (cueModel != null)
        {
            cueModel.SetActive(false);
        }

        if (aimingLine != null)
        {
            aimingLine.enabled = false;
        }
    }
}